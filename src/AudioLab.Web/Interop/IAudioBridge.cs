namespace AudioLab.Web.Interop;

/// <summary>The page's audio: a context, the worklet node that plays what the engine renders, and the worker that can host the engine.</summary>
public interface IAudioBridge
{
    /// <summary>Whether the page is cross-origin isolated.</summary>
    public bool CrossOriginIsolated { get; }

    /// <summary>What <c>typeof SharedArrayBuffer</c> says in the page.</summary>
    public string SharedArrayBufferType { get; }

    /// <summary>Whether the page is inside a frame.</summary>
    public bool InFrame { get; }

    /// <summary>The context's sample rate.</summary>
    public double SampleRate { get; }

    /// <summary>The context's base latency in seconds.</summary>
    public double BaseLatency { get; }

    /// <summary>The context's output latency in seconds, 0 where the browser does not report it.</summary>
    public double OutputLatency { get; }

    /// <summary>The context's state.</summary>
    public string State { get; }

    /// <summary>The browser's own underrun statistics as JSON, or null where it has none.</summary>
    public string? PlaybackStatsJson { get; }

    /// <summary>Creates the audio context.</summary>
    public void CreateContext();

    /// <summary>Loads the worklet module.</summary>
    public Task AddModuleAsync(string url);

    /// <summary>Creates the worklet node with a ring of <paramref name="capacityFrames" /> frames and chunks of <paramref name="chunkFrames" /> frames, and connects it to the output.</summary>
    public void CreateNode(int capacityFrames, int chunkFrames);

    /// <summary>Resumes the context; needs a user gesture on the page.</summary>
    public Task ResumeAsync();

    /// <summary>Calls back for every message the worklet sends the page.</summary>
    public void OnNodeMessage(Action<AudioMessage> handler);

    /// <summary>Copies a rendered chunk to the worklet.</summary>
    public void PostChunk(byte[] chunk);

    /// <summary>Tells the worklet that the page is its producer and it may ask for the first chunks.</summary>
    public void StartMainThreadProducer();

    /// <summary>Starts the worker. Its runtime boots by itself and says "booted" when the exports are ready.</summary>
    public void CreateWorker(string scriptUrl);

    /// <summary>Tells a booted worker to load the clips and build the engine; it answers with a text message.</summary>
    public void InitWorker(string origin, int sampleRate, int chunkFrames, int clipCount);

    /// <summary>Calls back for every message the worker sends the page.</summary>
    public void OnWorkerMessage(Action<AudioMessage> handler);

    /// <summary>Gives the worker and the worklet the two ends of one channel, so chunks go between them without passing through the page.</summary>
    public void ConnectWorkerToNode();

    /// <summary>Makes the worker's next render hold its thread for this long.</summary>
    public void StallWorker(int milliseconds);
}
