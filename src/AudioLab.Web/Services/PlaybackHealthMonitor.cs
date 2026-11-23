using AudioLab.Core.Playback;

namespace AudioLab.Web.Services;

/// <summary>Turns what the worklet and the worker report into whether the engine keeps up, remembering a miss for a few seconds.</summary>
public sealed class PlaybackHealthMonitor(TimeProvider time)
{
    private static readonly TimeSpan _memory = TimeSpan.FromSeconds(10);

    // A chunk that takes more of its time than this leaves too little for the next one to be late.
    private const double StrainedShare = 0.75;

    private DateTimeOffset? _dropped;
    private DateTimeOffset? _strained;
    private long _events;
    private long _overruns;
    private bool _seen;

    /// <summary>Whether the engine keeps up now, counting a dropout or a strained chunk of the last ten seconds.</summary>
    public PlaybackHealth Health
    {
        get
        {
            DateTimeOffset now = time.GetUtcNow();
            return Recent(_dropped, now) ? PlaybackHealth.Dropping : Recent(_strained, now) ? PlaybackHealth.Strained : PlaybackHealth.Steady;
        }
    }

    /// <summary>A sentence for the person at the controls, or null while all is well.</summary>
    public string? Message => Health switch
    {
        PlaybackHealth.Dropping => "Playback is dropping out: the audio engine in this page could not keep up with this project. "
            + "Mixing on the server instead is not built yet, so try fewer effects or tracks.",
        PlaybackHealth.Strained => "Playback is close to dropping out: the engine is using most of the time it has for each chunk of audio.",
        _ => null,
    };

    /// <summary>Notes the counters of the audio thread: a dropout event more than before is a miss.</summary>
    public void Observe(WorkletStats stats)
    {
        ArgumentNullException.ThrowIfNull(stats);
        if (_seen && stats.Events > _events)
        {
            _dropped = time.GetUtcNow();
        }
        _events = stats.Events;
        _seen = true;
    }

    /// <summary>Notes how long the engine took over its chunks: close to the time a chunk has, or over it, is strain.</summary>
    public void Observe(HostStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);
        double budget = PlaybackFormat.ChunkFrames * 1000.0 / status.SampleRate;
        if (status.MaxRenderMs > budget * StrainedShare || status.Overruns > _overruns)
        {
            _strained = time.GetUtcNow();
        }
        _overruns = status.Overruns;
    }

    /// <summary>Forgets everything, for an engine that starts again.</summary>
    public void Reset()
    {
        _dropped = null;
        _strained = null;
        _events = 0;
        _overruns = 0;
        _seen = false;
    }

    private static bool Recent(DateTimeOffset? at, DateTimeOffset now) => at is { } moment && now - moment < _memory;
}
