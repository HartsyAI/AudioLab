using System.Runtime.InteropServices.JavaScript;

namespace AudioLab.Web.Interop;

/// <summary>The audio context, worklet node and worker, reached through browser built-ins and the one allowlisted shim script.</summary>
public sealed class AudioBridge : IAudioBridge, IDisposable
{
    private JSObject? _context;

    private JSObject? _node;

    private JSObject? _nodePort;

    private JSObject? _worker;

    /// <inheritdoc />
    public bool CrossOriginIsolated => JSHost.GlobalThis.GetPropertyAsBoolean("crossOriginIsolated");

    /// <inheritdoc />
    public string SharedArrayBufferType => JSHost.GlobalThis.GetTypeOfProperty("SharedArrayBuffer");

    /// <inheritdoc />
    public bool InFrame
    {
        get
        {
            using JSObject top = JSHost.GlobalThis.GetPropertyAsJSObject("top")!;
            return !AudioImports.SameObject(top, JSHost.GlobalThis);
        }
    }

    /// <inheritdoc />
    public double SampleRate => Context.GetPropertyAsDouble("sampleRate");

    /// <inheritdoc />
    public double BaseLatency => Context.GetPropertyAsDouble("baseLatency");

    /// <inheritdoc />
    public double OutputLatency => Context.GetTypeOfProperty("outputLatency") == "number" ? Context.GetPropertyAsDouble("outputLatency") : 0;

    /// <inheritdoc />
    public string State => Context.GetPropertyAsString("state") ?? string.Empty;

    /// <inheritdoc />
    public string? PlaybackStatsJson
    {
        get
        {
            using JSObject? stats = Context.GetPropertyAsJSObject("playbackStats");
            return stats is null ? null : AudioImports.Stringify(stats);
        }
    }

    private JSObject Context => _context ?? throw new InvalidOperationException("The audio context has not been created.");

    private JSObject NodePort => _nodePort ?? throw new InvalidOperationException("The worklet node has not been created.");

    /// <inheritdoc />
    public void CreateContext()
    {
        using JSObject options = NewObject();
        options.SetProperty("latencyHint", "interactive");
        _context = AudioImports.Construct(Global("AudioContext"), [options]);
    }

    /// <inheritdoc />
    public async Task AddModuleAsync(string url)
    {
        using JSObject worklet = Context.GetPropertyAsJSObject("audioWorklet")!;
        await AudioImports.AddModule(worklet, url);
    }

    /// <inheritdoc />
    public void CreateNode(int capacityFrames, int chunkFrames)
    {
        using JSObject processorOptions = NewObject();
        processorOptions.SetProperty("capacity", capacityFrames);
        processorOptions.SetProperty("chunk", chunkFrames);
        using JSObject channels = AudioImports.ArrayOf(2);
        using JSObject nodeOptions = NewObject();
        nodeOptions.SetProperty("numberOfInputs", 0);
        nodeOptions.SetProperty("outputChannelCount", channels);
        nodeOptions.SetProperty("processorOptions", processorOptions);
        _node = AudioImports.Construct(Global("AudioWorkletNode"), [Context, "audiolab-engine", nodeOptions]);
        _nodePort = _node.GetPropertyAsJSObject("port");
        using JSObject destination = Context.GetPropertyAsJSObject("destination")!;
        using JSObject connected = AudioImports.Connect(_node, destination);
    }

    /// <inheritdoc />
    public Task ResumeAsync() => AudioImports.Resume(Context);

    /// <inheritdoc />
    public void OnNodeMessage(Action<AudioMessage> handler) => AudioImports.SetHandler(NodePort, "onmessage", e => handler(Read(e)));

    /// <inheritdoc />
    public void PostChunk(byte[] chunk)
    {
        using JSObject copy = AudioImports.CopyBytes(chunk);
        using JSObject buffer = copy.GetPropertyAsJSObject("buffer")!;
        AudioImports.PostToPort(NodePort, copy, [buffer]);
    }

    /// <inheritdoc />
    public void StartMainThreadProducer() => AudioImports.PostTextToPort(NodePort, "go");

    /// <inheritdoc />
    public void CreateWorker(string scriptUrl, string runtimeUrl, string origin, int sampleRate, int chunkFrames, int clipCount)
    {
        using JSObject options = NewObject();
        options.SetProperty("type", "module");
        _worker = AudioImports.Construct(Global("Worker"), [scriptUrl, options]);
        using JSObject init = NewObject();
        init.SetProperty("runtime", runtimeUrl);
        init.SetProperty("origin", origin);
        init.SetProperty("rate", sampleRate);
        init.SetProperty("chunk", chunkFrames);
        init.SetProperty("clips", clipCount);
        AudioImports.PostToWorker(_worker, init, []);
    }

    /// <inheritdoc />
    public void OnWorkerMessage(Action<AudioMessage> handler)
    {
        JSObject worker = _worker ?? throw new InvalidOperationException("The worker has not been created.");
        AudioImports.SetHandler(worker, "onmessage", e => handler(Read(e)));
        AudioImports.SetHandler(worker, "onerror", e => handler(new AudioMessage(0, "error:" + e.GetPropertyAsString("message"))));
    }

    /// <inheritdoc />
    public void ConnectWorkerToNode()
    {
        JSObject worker = _worker ?? throw new InvalidOperationException("The worker has not been created.");
        using JSObject channel = AudioImports.Construct(Global("MessageChannel"), []);
        using JSObject toWorklet = channel.GetPropertyAsJSObject("port1")!;
        using JSObject toWorker = channel.GetPropertyAsJSObject("port2")!;
        AudioImports.PostToPort(NodePort, toWorklet, [toWorklet]);
        using JSObject message = NewObject();
        message.SetProperty("port", toWorker);
        AudioImports.PostToWorker(worker, message, [toWorker]);
    }

    /// <inheritdoc />
    public void StallWorker(int milliseconds)
    {
        JSObject worker = _worker ?? throw new InvalidOperationException("The worker has not been created.");
        AudioImports.PostTextToWorker(worker, "stall:" + milliseconds);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _worker?.Dispose();
        _nodePort?.Dispose();
        _node?.Dispose();
        _context?.Dispose();
    }

    private static AudioMessage Read(JSObject message)
    {
        return message.GetTypeOfProperty("data") == "string"
            ? new AudioMessage(0, message.GetPropertyAsString("data"))
            : new AudioMessage(message.GetPropertyAsDouble("data"), null);
    }

    private static JSObject Global(string name) => JSHost.GlobalThis.GetPropertyAsJSObject(name)!;

    private static JSObject NewObject()
    {
        using JSObject constructor = Global("Object");
        return AudioImports.Construct(constructor, []);
    }
}
