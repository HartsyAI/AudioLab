using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using System.Text.Json;
using AudioLab.Core.Playback;

namespace AudioLab.Web.Interop;

/// <summary>The audio context, the worklet node and the engine's worker, reached through browser built-ins and the allowlisted shim script.</summary>
/// <remarks>
/// The worker renders 512-frame chunks and the worklet keeps a ring of them, asking for one more for each it plays, so no more than the ring
/// is ever queued. They share a transferred <c>MessagePort</c>: chunks never pass through this thread. There is no <c>SharedArrayBuffer</c>,
/// so the page needs no cross-origin isolation.
/// </remarks>
[SupportedOSPlatform("browser")]
public sealed class AudioHost : IAudioHost
{
    /// <summary>Frames in each chunk.</summary>
    public const int ChunkFrames = PlaybackFormat.ChunkFrames;

    private const string ShimAddress = "audio-shim.js";
    private const int MinSlots = 3;
    private const int MaxSlots = 16;
    private static readonly TimeSpan _bootTimeout = TimeSpan.FromSeconds(60);

    private readonly Dictionary<int, TaskCompletionSource> _calls = [];
    private JSObject? _context;
    private JSObject? _node;
    private JSObject? _nodePort;
    private JSObject? _worker;
    private TaskCompletionSource? _booted;
    private int _nextCall;

    /// <inheritdoc />
    public event Action<HostStatus>? StatusReceived;

    /// <inheritdoc />
    public event Action<WorkletStats>? WorkletReported;

    /// <inheritdoc />
    public event Action<string>? Failed;

    /// <inheritdoc />
    public bool Running { get; private set; }

    /// <inheritdoc />
    public bool Available =>
        JSHost.GlobalThis.GetTypeOfProperty("AudioContext") == "function"
        && JSHost.GlobalThis.GetTypeOfProperty("AudioWorkletNode") == "function"
        && JSHost.GlobalThis.GetTypeOfProperty("Worker") == "function";

    /// <inheritdoc />
    public int SampleRate { get; private set; }

    /// <inheritdoc />
    public double BufferMilliseconds { get; private set; }

    /// <inheritdoc />
    public async Task StartAsync(string baseAddress, int budgetMilliseconds, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseAddress);
        if (Running)
        {
            return;
        }
        if (!Available)
        {
            throw new AudioHostException("This browser has no audio worklet or web worker, so the engine cannot run in it.");
        }
        // The context is made before the first await: the browser lets it start only while the click that asked for it is still being handled.
        using JSObject options = NewObject();
        options.SetProperty("latencyHint", "interactive");
        _context = AudioImports.Construct(Global("AudioContext"), [options]);
        SampleRate = (int)_context.GetPropertyAsDouble("sampleRate");
        try
        {
            using JSObject worklet = _context.GetPropertyAsJSObject("audioWorklet")!;
            await AudioImports.AddModule(worklet, ShimAddress);
            await AudioImports.Resume(_context);
            int slots = SlotsFor(budgetMilliseconds);
            CreateNode(slots * ChunkFrames);
            BufferMilliseconds = ((slots * ChunkFrames * 1000.0) / SampleRate) + (LatencySeconds * 1000);
            await BootWorkerAsync(baseAddress, cancellationToken);
            ConnectWorkerToNode();
            Running = true;
        }
        catch (JSException exception)
        {
            Dispose();
            throw new AudioHostException($"The audio engine could not start: {exception.Message}");
        }
    }

    /// <inheritdoc />
    public Task SetDocumentAsync(string json, long revision) => CallAsync("SetDocument", json, (double)revision);

    /// <inheritdoc />
    public void Play() => _ = CallAsync("Play");

    /// <inheritdoc />
    public void Pause() => _ = CallAsync("Pause");

    /// <inheritdoc />
    public void Stop() => _ = CallAsync("Stop");

    /// <inheritdoc />
    public void Seek(double seconds) => _ = CallAsync("Seek", seconds);

    /// <inheritdoc />
    public void SetLoop(bool on, double startSeconds, double endSeconds) => _ = CallAsync("SetLoop", on, startSeconds, endSeconds);

    /// <inheritdoc />
    public void Dispose()
    {
        Running = false;
        if (_worker is not null)
        {
            AudioImports.Terminate(_worker);
        }
        _worker?.Dispose();
        _nodePort?.Dispose();
        _node?.Dispose();
        if (_context is not null)
        {
            _ = AudioImports.Close(_context);
        }
        _context?.Dispose();
        _worker = null;
        _nodePort = null;
        _node = null;
        _context = null;
    }

    // The ring takes what the budget leaves after the context's own latency, a whole number of chunks.
    private int SlotsFor(int budgetMilliseconds)
    {
        double frames = Math.Max(0, (budgetMilliseconds / 1000.0) - LatencySeconds) * SampleRate;
        return Math.Clamp((int)(frames / ChunkFrames), MinSlots, MaxSlots);
    }

    private double LatencySeconds
    {
        get
        {
            JSObject context = _context ?? throw new InvalidOperationException("The audio context has not been created.");
            double output = context.GetTypeOfProperty("outputLatency") == "number" ? context.GetPropertyAsDouble("outputLatency") : 0;
            return context.GetPropertyAsDouble("baseLatency") + output;
        }
    }

    private void CreateNode(int capacityFrames)
    {
        JSObject context = _context!;
        using JSObject processorOptions = NewObject();
        processorOptions.SetProperty("capacity", capacityFrames);
        processorOptions.SetProperty("chunk", ChunkFrames);
        using JSObject channels = AudioImports.ArrayOf(2);
        using JSObject nodeOptions = NewObject();
        nodeOptions.SetProperty("numberOfInputs", 0);
        nodeOptions.SetProperty("outputChannelCount", channels);
        nodeOptions.SetProperty("processorOptions", processorOptions);
        _node = AudioImports.Construct(Global("AudioWorkletNode"), [context, "audiolab-engine", nodeOptions]);
        _nodePort = _node.GetPropertyAsJSObject("port");
        using JSObject destination = context.GetPropertyAsJSObject("destination")!;
        using JSObject connected = AudioImports.Connect(_node, destination);
        AudioImports.SetHandler(_nodePort!, "onmessage", OnWorkletMessage);
    }

    private async Task BootWorkerAsync(string baseAddress, CancellationToken cancellationToken)
    {
        _booted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using JSObject options = NewObject();
        options.SetProperty("type", "classic");
        _worker = AudioImports.Construct(Global("Worker"), [ShimAddress, options]);
        AudioImports.SetHandler(_worker, "onmessage", OnWorkerMessage);
        AudioImports.SetHandler(_worker, "onerror", error => _booted.TrySetException(new AudioHostException(
            $"The engine's worker failed: {error.GetPropertyAsString("message")}")));
        await _booted.Task.WaitAsync(_bootTimeout, cancellationToken);
        await CallAsync("Start", baseAddress, SampleRate, ChunkFrames);
    }

    private void ConnectWorkerToNode()
    {
        using JSObject channel = AudioImports.Construct(Global("MessageChannel"), []);
        using JSObject toWorklet = channel.GetPropertyAsJSObject("port1")!;
        using JSObject toWorker = channel.GetPropertyAsJSObject("port2")!;
        AudioImports.PostToPort(_nodePort!, toWorklet, [toWorklet]);
        using JSObject message = NewObject();
        message.SetProperty("port", toWorker);
        AudioImports.PostToWorker(_worker!, message, [toWorker]);
    }

    private Task CallAsync(string call, params object?[] arguments)
    {
        JSObject worker = _worker ?? throw new InvalidOperationException("The engine's worker has not been created.");
        int id = ++_nextCall;
        TaskCompletionSource done = new(TaskCreationOptions.RunContinuationsAsynchronously);
        _calls[id] = done;
        using JSObject message = NewObject();
        message.SetProperty("id", id);
        message.SetProperty("call", call);
        using JSObject list = NewArray();
        foreach (object? argument in arguments)
        {
            AudioImports.Push(list, argument);
        }
        message.SetProperty("args", list);
        AudioImports.PostToWorker(worker, message, []);
        return done.Task;
    }

    private void OnWorkletMessage(JSObject message)
    {
        string? text = message.GetPropertyAsString("data");
        if (text is not null && JsonSerializer.Deserialize(text, PlaybackJsonContext.Default.WorkletStats) is { } stats)
        {
            WorkletReported?.Invoke(stats);
        }
    }

    private void OnWorkerMessage(JSObject message)
    {
        using JSObject data = message.GetPropertyAsJSObject("data")!;
        if (data.GetTypeOfProperty("status") == "string")
        {
            if (JsonSerializer.Deserialize(data.GetPropertyAsString("status")!, PlaybackJsonContext.Default.HostStatus) is { } status)
            {
                StatusReceived?.Invoke(status);
            }
        }
        else if (data.GetTypeOfProperty("id") == "number")
        {
            Finish((int)data.GetPropertyAsDouble("id"), data.GetTypeOfProperty("error") == "string" ? data.GetPropertyAsString("error") : null);
        }
        else if (data.GetTypeOfProperty("booted") == "boolean")
        {
            _booted?.TrySetResult();
        }
        else if (data.GetTypeOfProperty("error") == "string")
        {
            string reason = data.GetPropertyAsString("error")!;
            if (_booted is { Task.IsCompleted: false })
            {
                _booted.TrySetException(new AudioHostException($"The engine's worker could not start: {reason}"));
            }
            else
            {
                Failed?.Invoke(reason);
            }
        }
    }

    private void Finish(int id, string? error)
    {
        if (!_calls.Remove(id, out TaskCompletionSource? done))
        {
            return;
        }
        if (error is null)
        {
            done.TrySetResult();
        }
        else
        {
            done.TrySetException(new AudioHostException(error));
            Failed?.Invoke(error);
        }
    }

    private static JSObject Global(string name) => JSHost.GlobalThis.GetPropertyAsJSObject(name)!;

    private static JSObject NewObject()
    {
        using JSObject constructor = Global("Object");
        return AudioImports.Construct(constructor, []);
    }

    private static JSObject NewArray()
    {
        using JSObject constructor = Global("Array");
        return AudioImports.Construct(constructor, []);
    }
}
