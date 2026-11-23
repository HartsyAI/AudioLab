using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using System.Text.Json;
using AudioLab.Core.Playback;

namespace AudioLab.Web.Interop;

/// <summary>The audio context, the worklet node and the engine's worker, reached through browser built-ins and the allowlisted shim script.</summary>
/// <remarks>
/// The worker renders 512-frame chunks and the worklet keeps a ring of them, asking for one more for each it plays, so no more than the ring
/// is ever queued. They share a transferred <c>MessagePort</c>: chunks never pass through this thread. There is no <c>SharedArrayBuffer</c>,
/// so the page needs no cross-origin isolation. A worker that crashes after it started ends the engine: the calls that wait are failed,
/// everything is let go and <see cref="Failed"/> says why; <see cref="StartAsync"/> may then be called again.
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

    private readonly PendingCalls _calls = new();
    private JSObject? _context;
    private JSObject? _node;
    private JSObject? _nodePort;
    private JSObject? _worker;
    private TaskCompletionSource? _booted;

    /// <inheritdoc />
    public event Action<HostStatus>? StatusReceived;

    /// <inheritdoc />
    public event Action<WorkletStats>? WorkletReported;

    /// <inheritdoc />
    public event Action<string>? Failed;

    /// <inheritdoc />
    public event Action<string>? Rejected;

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

    private double LatencySeconds
    {
        get
        {
            JSObject context = _context ?? throw new InvalidOperationException("The audio context has not been created.");
            double output = context.GetTypeOfProperty("outputLatency") == "number" ? context.GetPropertyAsDouble("outputLatency") : 0;
            return context.GetPropertyAsDouble("baseLatency") + output;
        }
    }

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
        Teardown();
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
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            // Whatever stopped the start, the context, the node and the worker made so far are let go, so a second try starts from nothing.
            Teardown();
            throw exception as AudioHostException ?? new AudioHostException(Reason(exception));
        }
    }

    /// <inheritdoc />
    public Task SetDocumentAsync(string json, long revision) =>
        Running ? CallAsync("SetDocument", json, (double)revision) : Task.FromException(new AudioHostException("The audio engine is not running."));

    /// <inheritdoc />
    public void Play() => Send("Play");

    /// <inheritdoc />
    public void Pause() => Send("Pause");

    /// <inheritdoc />
    public void Stop() => Send("Stop");

    /// <inheritdoc />
    public void Seek(double seconds) => Send("Seek", seconds);

    /// <inheritdoc />
    public void SetLoop(bool on, double startSeconds, double endSeconds) => Send("SetLoop", on, startSeconds, endSeconds);

    /// <inheritdoc />
    public void Dispose()
    {
        _calls.FailAll("The audio engine was stopped.");
        Teardown();
    }

    private static string Reason(Exception exception) => exception switch
    {
        TimeoutException => "The engine's worker did not start within a minute.",
        JSException => $"The audio engine could not start: {exception.Message}",
        _ => $"The audio engine could not start: {exception.GetType().Name}: {exception.Message}",
    };

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

    // The ring takes what the budget leaves after the context's latency as it was at the start, a whole number of chunks.
    private int SlotsFor(int budgetMilliseconds)
    {
        double frames = Math.Max(0, (budgetMilliseconds / 1000.0) - LatencySeconds) * SampleRate;
        return Math.Clamp((int)(frames / ChunkFrames), MinSlots, MaxSlots);
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
        TaskCompletionSource booted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        _booted = booted;
        using JSObject options = NewObject();
        options.SetProperty("type", "classic");
        _worker = AudioImports.Construct(Global("Worker"), [ShimAddress, options]);
        AudioImports.SetHandler(_worker, "onmessage", OnWorkerMessage);
        AudioImports.SetHandler(_worker, "onerror", OnWorkerError);
        await booted.Task.WaitAsync(_bootTimeout, cancellationToken);
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

    // A call the caller does not wait for: if the worker refuses it, the reason is reported and the engine goes on.
    private void Send(string call, params object?[] arguments)
    {
        if (!Running)
        {
            return;
        }
        Task done = CallAsync(call, arguments);
        _ = done.ContinueWith(
            task => Rejected?.Invoke(task.Exception!.GetBaseException().Message),
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Current);
    }

    private Task CallAsync(string call, params object?[] arguments)
    {
        JSObject worker = _worker ?? throw new AudioHostException("The audio engine is not running.");
        (int id, Task done) = _calls.Begin();
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
        return done;
    }

    private void OnWorkletMessage(JSObject message)
    {
        using JSObject released = message;
        using JSObject data = message.GetPropertyAsJSObject("data")!;
        WorkletReported?.Invoke(new WorkletStats(
            data.GetPropertyAsDouble("0"),
            (int)data.GetPropertyAsDouble("1"),
            (int)data.GetPropertyAsDouble("2"),
            (long)data.GetPropertyAsDouble("3"),
            (long)data.GetPropertyAsDouble("4"),
            data.GetPropertyAsDouble("5") == 1));
    }

    private void OnWorkerMessage(JSObject message)
    {
        using JSObject released = message;
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
            string? error = data.GetTypeOfProperty("error") == "string" ? data.GetPropertyAsString("error") : null;
            _calls.Complete((int)data.GetPropertyAsDouble("id"), error);
        }
        else if (data.GetTypeOfProperty("booted") == "boolean")
        {
            _booted?.TrySetResult();
        }
        else if (data.GetTypeOfProperty("error") == "string")
        {
            Crash($"The engine's worker stopped: {data.GetPropertyAsString("error")}");
        }
    }

    private void OnWorkerError(JSObject error)
    {
        using JSObject released = error;
        Crash($"The engine's worker failed: {error.GetPropertyAsString("message")}");
    }

    // Before the engine runs, a worker that fails fails the start; after, it ends the engine, and nothing it was asked is left waiting.
    private void Crash(string reason)
    {
        if (_booted is { Task.IsCompleted: false } booted)
        {
            booted.TrySetException(new AudioHostException(reason));
            return;
        }
        bool wasRunning = Running;
        _calls.FailAll(reason);
        Teardown();
        if (wasRunning)
        {
            Failed?.Invoke(reason);
        }
    }

    private void Teardown()
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
        _booted = null;
    }
}
