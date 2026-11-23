using AudioLab.Core.Playback;

namespace AudioLab.Web.Interop;

/// <summary>The engine as the page sees it: an audio context, a worker that renders, and the messages between them (D24).</summary>
public interface IAudioHost : IDisposable
{
    /// <summary>Raised about every 40 ms with what the worker reports of the transport and the rendering.</summary>
    public event Action<HostStatus>? StatusReceived;

    /// <summary>Raised about every 20 ms with what the worklet counts on the audio thread.</summary>
    public event Action<WorkletStats>? WorkletReported;

    /// <summary>Raised when the worker fails after it has started, with the reason.</summary>
    public event Action<string>? Failed;

    /// <summary>Whether the engine is running.</summary>
    public bool Running { get; }

    /// <summary>Whether this browser can run the engine at all: it has the audio and worker built-ins.</summary>
    public bool Available { get; }

    /// <summary>The audio context's sample rate, once started.</summary>
    public int SampleRate { get; }

    /// <summary>The output buffering the worklet's ring and the context's own latency add up to, in milliseconds, once started.</summary>
    public double BufferMilliseconds { get; }

    /// <summary>Creates the audio context, loads the worklet, boots the worker and connects the two; must be called from a user gesture.</summary>
    /// <param name="baseAddress">The page's address with its path base, which the worker fetches clips from.</param>
    /// <param name="budgetMilliseconds">The most output buffering to add up to, the context's own latency included.</param>
    /// <param name="cancellationToken">Stops waiting for the worker.</param>
    /// <exception cref="AudioHostException">The engine could not be started.</exception>
    public Task StartAsync(string baseAddress, int budgetMilliseconds, CancellationToken cancellationToken);

    /// <summary>Gives the worker the project to play; the task ends when its plan is in place or has failed.</summary>
    public Task SetDocumentAsync(string json, long revision);

    /// <summary>Starts playing.</summary>
    public void Play();

    /// <summary>Pauses.</summary>
    public void Pause();

    /// <summary>Stops and returns to where playing began.</summary>
    public void Stop();

    /// <summary>Moves the playhead to a time in seconds.</summary>
    public void Seek(double seconds);

    /// <summary>Turns the loop on or off over a range of seconds.</summary>
    public void SetLoop(bool on, double startSeconds, double endSeconds);
}
