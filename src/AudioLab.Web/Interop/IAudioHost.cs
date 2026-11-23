using AudioLab.Core.Playback;

namespace AudioLab.Web.Interop;

/// <summary>The engine as the page sees it: an audio context, a worker that renders, and the messages between them (D24).</summary>
public interface IAudioHost : IDisposable
{
    /// <summary>Raised about every 40 ms with what the worker reports of the transport and the rendering.</summary>
    public event Action<HostStatus>? StatusReceived;

    /// <summary>Raised about 25 times a second with what the worklet counts on the audio thread.</summary>
    public event Action<WorkletStats>? WorkletReported;

    /// <summary>Raised when the engine stops after it started (its worker crashed), with the reason; <see cref="Running"/> is then false.</summary>
    public event Action<string>? Failed;

    /// <summary>Raised when the worker refuses a call with the reason; the engine goes on.</summary>
    public event Action<string>? Rejected;

    /// <summary>Whether the engine is running.</summary>
    public bool Running { get; }

    /// <summary>Whether this browser can run the engine at all: it has the audio and worker built-ins.</summary>
    public bool Available { get; }

    /// <summary>The audio context's sample rate, once started.</summary>
    public int SampleRate { get; }

    /// <summary>The output buffering the worklet's ring and the context's latency at the start add up to, in milliseconds, once started.</summary>
    public double BufferMilliseconds { get; }

    /// <summary>Creates the audio context, loads the worklet, boots the worker and connects the two; must be called from a user gesture.</summary>
    /// <remarks>A failed start leaves nothing behind, and a stopped engine can be started again.</remarks>
    /// <param name="baseAddress">The page's address with its path base, which the worker fetches clips from.</param>
    /// <param name="budgetMilliseconds">The most output buffering to add up to, the context's own latency included.</param>
    /// <param name="cancellationToken">Stops waiting for the worker.</param>
    /// <exception cref="AudioHostException">The engine could not be started.</exception>
    public Task StartAsync(string baseAddress, int budgetMilliseconds, CancellationToken cancellationToken);

    /// <summary>Gives the worker the project to play; the task ends when its plan is in place or has failed.</summary>
    /// <exception cref="AudioHostException">The engine is not running, or it refused the document.</exception>
    public Task SetDocumentAsync(string json, long revision);

    /// <summary>Starts playing; nothing happens when the engine is not running.</summary>
    public void Play();

    /// <summary>Pauses; nothing happens when the engine is not running.</summary>
    public void Pause();

    /// <summary>Stops and returns to where playing began; nothing happens when the engine is not running.</summary>
    public void Stop();

    /// <summary>Moves the playhead to a time in seconds; nothing happens when the engine is not running.</summary>
    public void Seek(double seconds);

    /// <summary>Turns the loop on or off over a range of seconds; nothing happens when the engine is not running.</summary>
    public void SetLoop(bool on, double startSeconds, double endSeconds);
}
