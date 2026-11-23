using AudioLab.Core.Model;
using AudioLab.Core.Playback;
using AudioLab.Core.Rules;
using AudioLab.Web.Interop;

namespace AudioLab.Web.Services;

/// <summary>The page playback: it starts the engine on the first Play, keeps its project current with the open one and tracks the playhead.</summary>
/// <remarks>
/// The engine runs in a worker (D24); this class holds no audio logic. Blazor WebAssembly runs on one thread, so nothing here locks.
/// What is asked before the engine runs (a seek, a loop) is kept and applied when it starts.
/// </remarks>
public sealed class PlaybackService : IDisposable
{
    // How much of a level is left after one status (40 ms): about 35 dB a second.
    private const double Fallback = 0.85;

    private readonly IAudioHost _host;
    private readonly HttpClient _http;
    private readonly PlaybackHealthMonitor _health;
    private LiveProject? _project;
    private LiveProject? _sent;
    private long _sentRevision = -1;
    private long _sequence;
    private double _requested;
    private bool _loopOn;
    private (double Start, double End)? _sentLoop;
    private WorkletStats? _worklet;
    private HostStatus? _status;
    private LevelPair _master;
    private readonly Dictionary<string, LevelPair> _levels = [];

    /// <summary>Creates the service over the engine the browser provides.</summary>
    public PlaybackService(IAudioHost host, HttpClient http, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(time);
        _host = host;
        _http = http;
        _health = new PlaybackHealthMonitor(time);
        host.StatusReceived += OnStatus;
        host.WorkletReported += OnWorklet;
        host.Failed += OnFailed;
    }

    /// <summary>Raised when the transport, the playhead, the health or a problem changed; about every 40 ms while the engine runs.</summary>
    public event Action? Changed;

    /// <summary>Whether the engine runs.</summary>
    public PlaybackPhase Phase { get; private set; }

    /// <summary>Whether this browser can run the engine at all.</summary>
    public bool Available => _host.Available;

    /// <summary>Whether the engine is playing.</summary>
    public TransportState Transport => _status?.State ?? TransportState.Stopped;

    /// <summary>The playhead in seconds: what the audio thread is about to play, or where the playhead was last put.</summary>
    public double Position =>
        _worklet is { Primed: true } worklet && _status is { } status ? Folded(worklet.Position / status.SampleRate) : _requested;

    /// <summary>Whether the loop is on.</summary>
    public bool Looping => _loopOn;

    /// <summary>The loop's start and end in seconds while it is on: the whole of what is heard, until a range can be chosen.</summary>
    public (double Start, double End)? LoopRange =>
        _loopOn && _project?.Document is { } document && ProjectContent.End(document, audibleOnly: true) is > 0 and var end ? (0, end) : null;

    /// <summary>The master's level now, falling after a peak.</summary>
    public LevelPair Master => _master;

    /// <summary>How many clips the engine is fetching or preparing for the newest document.</summary>
    public int Loading => _status?.Loading ?? 0;

    /// <summary>Why the engine or the newest document cannot be played, or null.</summary>
    public string? Problem { get; private set; }

    /// <summary>Whether the engine keeps up with the audio device.</summary>
    public PlaybackHealth Health => _health.Health;

    /// <summary>Whether the engine runs ahead-of-time compiled code; false until the engine has reported.</summary>
    public bool CompiledAhead => _status?.CompiledAhead ?? false;

    /// <summary>A sentence for a project the engine, as built, is unlikely to keep up with, or null: the warning comes before the dropout.</summary>
    public string? LoadWarning => _status is not null && _project?.Document is { } document ? EngineLoadAdvice.Warning(document, CompiledAhead) : null;

    /// <summary>A sentence about a miss for the person at the controls, or null.</summary>
    public string? HealthMessage => _health.Message;

    /// <summary>How many times the audio thread ran out of audio since the engine started: a run of quanta without audio is one.</summary>
    public long Dropouts => _worklet?.Events ?? 0;

    /// <summary>The longest time the engine took to render one chunk since it started, in milliseconds.</summary>
    public double LongestRenderMilliseconds { get; private set; }

    /// <summary>The longest time between two chunks the engine rendered since it started, in milliseconds.</summary>
    public double LongestGapMilliseconds { get; private set; }

    /// <summary>The audio context's sample rate, once the engine runs.</summary>
    public int SampleRate => _host.SampleRate;

    /// <summary>The output buffering in milliseconds, once the engine runs.</summary>
    public double BufferMilliseconds => _host.BufferMilliseconds;

    /// <summary>Follows the open project: its document goes to the engine on every revision, now and from here on.</summary>
    public void Follow(LiveProject project)
    {
        ArgumentNullException.ThrowIfNull(project);
        Unfollow();
        _project = project;
        project.Changed += OnProjectChanged;
        Push();
    }

    /// <summary>Stops following, and stops playing: the engine's project belongs to the one that was open.</summary>
    public void Unfollow()
    {
        if (_project is null)
        {
            return;
        }
        _project.Changed -= OnProjectChanged;
        _project = null;
        _sent = null;
        _sentRevision = -1;
        if (Phase == PlaybackPhase.Ready)
        {
            _host.Stop();
        }
    }

    /// <summary>The level of a track now, falling after a peak.</summary>
    public LevelPair LevelOf(TrackId track) => _levels.GetValueOrDefault(track.Value);

    /// <summary>Starts playing from the playhead, starting the engine first if it is not running; call it from the click that asks.</summary>
    public async Task PlayAsync()
    {
        if (Phase != PlaybackPhase.Ready && !await StartAsync())
        {
            return;
        }
        _host.Play();
    }

    /// <summary>Pauses; the playhead stays.</summary>
    public void Pause()
    {
        if (Phase == PlaybackPhase.Ready)
        {
            _host.Pause();
        }
    }

    /// <summary>Stops and returns the playhead to where playing began.</summary>
    public void Stop()
    {
        if (Phase == PlaybackPhase.Ready)
        {
            _host.Stop();
        }
    }

    /// <summary>Moves the playhead to a time in seconds.</summary>
    public void Seek(double seconds)
    {
        _requested = Math.Max(0, seconds);
        if (Phase == PlaybackPhase.Ready)
        {
            _host.Seek(_requested);
        }
        Changed?.Invoke();
    }

    /// <summary>Plays or pauses, as the play button and the space bar do.</summary>
    public Task TogglePlayAsync()
    {
        if (Transport != TransportState.Playing)
        {
            return PlayAsync();
        }
        Pause();
        return Task.CompletedTask;
    }

    /// <summary>Turns the loop over the whole of the project on or off.</summary>
    public void SetLoop(bool on)
    {
        _loopOn = on;
        SyncLoop();
        Changed?.Invoke();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Unfollow();
        _host.StatusReceived -= OnStatus;
        _host.WorkletReported -= OnWorklet;
        _host.Failed -= OnFailed;
        _host.Dispose();
    }

    private async Task<bool> StartAsync()
    {
        if (Phase == PlaybackPhase.Starting)
        {
            return false;
        }
        Phase = PlaybackPhase.Starting;
        Problem = null;
        LongestRenderMilliseconds = 0;
        LongestGapMilliseconds = 0;
        _health.Reset();
        Changed?.Invoke();
        try
        {
            await _host.StartAsync(_http.BaseAddress!.AbsoluteUri, PlaybackFormat.BudgetMilliseconds, CancellationToken.None);
        }
        catch (AudioHostException exception)
        {
            Phase = PlaybackPhase.Failed;
            Problem = exception.Message;
            Changed?.Invoke();
            return false;
        }
        Phase = PlaybackPhase.Ready;
        _host.Seek(_requested);
        _sentLoop = null;
        SyncLoop();
        Push();
        Changed?.Invoke();
        return true;
    }

    private void Push()
    {
        if (Phase != PlaybackPhase.Ready || _project?.Document is not { } document)
        {
            return;
        }
        if (ReferenceEquals(_sent, _project) && _sentRevision == _project.Revision)
        {
            return;
        }
        _sent = _project;
        _sentRevision = _project.Revision;
        _ = SendAsync(document, ++_sequence);
        SyncLoop();
    }

    // A chunk is tagged with its first frame, so the frames after a loop wrap inside it read past the loop's end: they fold back to its start.
    private double Folded(double seconds) =>
        LoopRange is { } range && seconds >= range.End && range.End > range.Start
            ? range.Start + ((seconds - range.End) % (range.End - range.Start))
            : seconds;

    // The loop's range follows what the project holds, so an edit that lengthens it moves the loop's end.
    private void SyncLoop()
    {
        if (Phase != PlaybackPhase.Ready)
        {
            return;
        }
        (double Start, double End)? range = LoopRange;
        if (range == _sentLoop)
        {
            return;
        }
        _sentLoop = range;
        _host.SetLoop(range is not null, range?.Start ?? 0, range?.End ?? 0);
    }

    private async Task SendAsync(ProjectDocument document, long sequence)
    {
        try
        {
            await _host.SetDocumentAsync(ProjectJson.Write(document), sequence);
        }
        catch (AudioHostException exception)
        {
            Problem = exception.Message;
            Changed?.Invoke();
        }
    }

    private void OnProjectChanged() => Push();

    private void OnStatus(HostStatus status)
    {
        _status = status;
        LongestRenderMilliseconds = Math.Max(LongestRenderMilliseconds, status.MaxRenderMs);
        LongestGapMilliseconds = Math.Max(LongestGapMilliseconds, status.LongestGapMs);
        Fall(status);
        _health.Observe(status);
        Problem = status.Problem ?? (Phase == PlaybackPhase.Failed ? Problem : null);
        Changed?.Invoke();
    }

    // A level jumps up to a new peak and falls back, so a short peak can be seen at the rate the status arrives.
    private void Fall(HostStatus status)
    {
        _master = Held(_master, status.Master);
        foreach (TrackLevel track in status.Tracks)
        {
            _levels[track.Track] = Held(_levels.GetValueOrDefault(track.Track), track.Level);
        }
    }

    private static LevelPair Held(LevelPair shown, LevelPair peak) =>
        new(Math.Max(peak.Left, shown.Left * Fallback), Math.Max(peak.Right, shown.Right * Fallback));

    private void OnWorklet(WorkletStats stats)
    {
        _worklet = stats;
        _health.Observe(stats);
    }

    private void OnFailed(string reason)
    {
        Phase = PlaybackPhase.Failed;
        Problem = reason;
        Changed?.Invoke();
    }
}
