using System.Globalization;
using AudioLab.Core.Playback;

namespace AudioLab.Web.Services;

/// <summary>Turns what the worklet and the worker report into whether the engine keeps up, remembering a miss for a few seconds.</summary>
public sealed class PlaybackHealthMonitor(TimeProvider time)
{
    // A chunk that takes more of its time than this leaves too little for the next one to be late.
    private const double StrainedShare = 0.75;

    // The clock is off when a window is more than this from the wall clock twice running, and right again when one is within the lower bound.
    private const double ClockTolerance = 0.02;
    private const double ClockSettled = 0.01;

    private static readonly TimeSpan _memory = TimeSpan.FromSeconds(10);

    // A window of this long is compared with the wall clock; a report that comes later than the gap after the one before it (the page
    // was suspended, or the context was) starts the window again. A page that is busy for a moment delays a report, so one window can
    // be a few per cent out without the clock being at fault, which is why two are needed.
    private static readonly TimeSpan _clockWindow = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan _clockGap = TimeSpan.FromSeconds(1);

    private DateTimeOffset? _dropped;
    private DateTimeOffset? _strained;
    private long _events;
    private long _overruns;
    private bool _seen;
    private int _rate;
    private long? _windowStart;
    private long _lastReport;
    private double _windowFrames;
    private int _offWindows;
    private bool _drifting;

    /// <summary>Whether the engine keeps up now, counting a dropout or a strained chunk of the last ten seconds.</summary>
    public PlaybackHealth Health
    {
        get
        {
            DateTimeOffset now = time.GetUtcNow();
            return Recent(_dropped, now) ? PlaybackHealth.Dropping
                : Recent(_strained, now) ? PlaybackHealth.Strained
                : Drifting ? PlaybackHealth.Drifting : PlaybackHealth.Steady;
        }
    }

    /// <summary>How fast the audio context ran against the wall clock over the last window (1 is right); null until one has passed.</summary>
    public double? ClockRatio { get; private set; }

    /// <summary>Whether the audio context ran more than 2% off the wall clock over the last two windows.</summary>
    public bool Drifting => _drifting;

    /// <summary>A sentence for the person at the controls, or null while all is well.</summary>
    public string? Message => Health switch
    {
        PlaybackHealth.Dropping => "Playback is dropping out: the audio engine in this page could not keep up with this project. "
            + "Mixing on the server instead is not built yet, so try fewer effects or tracks.",
        PlaybackHealth.Strained => "Playback is close to dropping out: the engine is using most of the time it has for each chunk of audio.",
        PlaybackHealth.Drifting => "Playback is at the wrong speed: the audio clock of this page ran at "
            + Math.Round((ClockRatio ?? 1) * 100).ToString(CultureInfo.InvariantCulture) + "% of real time over the last ten seconds. "
            + "No dropout shows it, and the cause is not known.",
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
        ObserveClock(stats);
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
        _rate = status.SampleRate;
    }

    /// <summary>Forgets everything, for an engine that starts again.</summary>
    public void Reset()
    {
        _dropped = null;
        _strained = null;
        _events = 0;
        _overruns = 0;
        _seen = false;
        _windowStart = null;
        ClockRatio = null;
        _offWindows = 0;
        _drifting = false;
    }

    // The worklet counts the frames it has played; over a window those are compared with the time the page saw pass.
    private void ObserveClock(WorkletStats stats)
    {
        long now = time.GetTimestamp();
        bool gap = time.GetElapsedTime(_lastReport, now) > _clockGap;
        _lastReport = now;
        if (_rate <= 0 || stats.Frames <= 0)
        {
            return;
        }
        if (_windowStart is not { } start || gap || stats.Frames < _windowFrames)
        {
            StartWindow(now, stats.Frames);
            _offWindows = 0;
            return;
        }
        TimeSpan elapsed = time.GetElapsedTime(start, now);
        if (elapsed < _clockWindow)
        {
            return;
        }
        double ratio = (stats.Frames - _windowFrames) / (_rate * elapsed.TotalSeconds);
        ClockRatio = ratio;
        double off = Math.Abs(ratio - 1);
        _offWindows = off > ClockTolerance ? _offWindows + 1 : 0;
        if (_offWindows >= 2)
        {
            _drifting = true;
        }
        else if (off <= ClockSettled)
        {
            _drifting = false;
        }
        StartWindow(now, stats.Frames);
    }

    private void StartWindow(long now, double frames)
    {
        _windowStart = now;
        _windowFrames = frames;
    }

    private static bool Recent(DateTimeOffset? at, DateTimeOffset now) => at is { } moment && now - moment < _memory;
}
