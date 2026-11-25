using AudioLab.Core.Commands;
using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Web.Session;

/// <summary>Records one pass of a track's automation while the transport plays (D25), fed by ticks and the control's values.</summary>
/// <remarks>
/// Write records every sample from the start of the pass to its stop, in one segment. Touch records each grab-to-release as its own
/// segment, so the old curve between two touches is kept. Latch records from the first touch to the stop, in one segment, and keeps the
/// value. Nothing is recorded in Read or Off. The rules of the points (D69): a sample whose time is before the last point is dropped, so a
/// loop or a seek back cannot interleave two laps; a sample at the last point's time replaces its value, so no two points share a time
/// and no zero-height step is written; and a pass holds at most <see cref="MaxPoints"/> points, after which it stops and says so.
/// </remarks>
public sealed class AutomationWriter(AutomationMode mode, AutomationParameter parameter)
{
    /// <summary>The most points one pass may hold: a commit of this many points is well inside the hub's message limit.</summary>
    public const int MaxPoints = 50_000;

    private readonly List<List<WrittenPoint>> _segments = [];

    private bool _passing;

    private bool _held;

    private bool _latched;

    private bool _truncated;

    /// <summary>The mode the pass records in.</summary>
    public AutomationMode Mode { get; } = mode is AutomationMode.Write or AutomationMode.Touch or AutomationMode.Latch
        ? mode
        : throw new ArgumentOutOfRangeException(nameof(mode), mode, "Only write, touch and latch record a pass.");

    /// <summary>The parameter the pass writes.</summary>
    public AutomationParameter Parameter { get; } = parameter;

    /// <summary>Whether the pass has begun and has not stopped.</summary>
    public bool Passing => _passing;

    /// <summary>Whether the next sample is recorded: the pass is running and, for touch and latch, the control has been touched.</summary>
    public bool Recording => _passing && !_truncated && (Mode == AutomationMode.Write || _held || _latched);

    /// <summary>The points written so far, across the segments.</summary>
    public int Count { get; private set; }

    /// <summary>Starts the pass when the transport starts playing, at a time in seconds with the control's value then.</summary>
    public void Begin(double time, double value)
    {
        _segments.Clear();
        Count = 0;
        _passing = true;
        _held = false;
        _latched = false;
        _truncated = false;
        if (Mode == AutomationMode.Write)
        {
            _segments.Add([]);
            Record(time, value);
        }
    }

    /// <summary>The control is touched at a time with a value; touch starts a segment, and touch and latch record here.</summary>
    public void Grab(double time, double value)
    {
        if (!_passing || Mode == AutomationMode.Write || _held)
        {
            return;
        }
        _held = true;
        _latched = Mode == AutomationMode.Latch;
        if (Mode == AutomationMode.Touch || _segments.Count == 0)
        {
            _segments.Add([]);
        }
        Record(time, value);
    }

    /// <summary>The control is let go at a time with a value; touch stops recording and keeps this last point.</summary>
    public void Release(double time, double value)
    {
        if (!_passing || !_held)
        {
            return;
        }
        Record(time, value);
        _held = false;
    }

    /// <summary>The playhead jumped (a seek, or a loop's wrap): a write or a held touch starts a new segment at the jump.</summary>
    /// <remarks>
    /// A write records its resting value at the jump; a held touch goes on at the value it holds. A latch holds its value across the skip, so it
    /// keeps its segment, and a touch that is not held has nothing to end. The caller samples at the jump as well.
    /// </remarks>
    public void Jump(double time, double value)
    {
        if (!_passing || _segments.Count == 0 || Mode == AutomationMode.Latch || (Mode == AutomationMode.Touch && !_held))
        {
            return;
        }
        _segments.Add([]);
        Record(time, value);
    }

    /// <summary>A sample of the control's value at a time, from a transport tick; recorded only while the pass records.</summary>
    public void Sample(double time, double value)
    {
        if (Recording)
        {
            Record(time, value);
        }
    }

    /// <summary>Stops the pass at a time, and returns what it recorded: the segments to commit, or null when nothing was written.</summary>
    public AutomationPass? Stop(double time, double value)
    {
        if (!_passing)
        {
            return null;
        }
        if (Recording)
        {
            Record(time, value);
        }
        _passing = false;
        _held = false;
        _latched = false;
        List<IReadOnlyList<WrittenPoint>> segments = [];
        foreach (List<WrittenPoint> segment in _segments)
        {
            if (segment.Count > 0)
            {
                segments.Add([.. segment]);
            }
        }
        return segments.Count == 0 ? null : new AutomationPass(segments, Mode == AutomationMode.Touch, _truncated);
    }

    private void Record(double time, double value)
    {
        if (_segments.Count == 0)
        {
            return;
        }
        double at = Math.Clamp(time, 0, ProjectLimits.MaxSeconds);
        double clamped = AutomationRules.Clamp(Parameter, value);
        if (LastTime() is { } previous && at < previous)
        {
            return;
        }
        List<WrittenPoint> segment = _segments[^1];
        if (segment.Count > 0 && segment[^1].Time == at)
        {
            segment[^1] = new WrittenPoint(at, clamped);
            return;
        }
        if (Count >= MaxPoints)
        {
            _truncated = true;
            return;
        }
        segment.Add(new WrittenPoint(at, clamped));
        Count++;
    }

    private double? LastTime()
    {
        for (int index = _segments.Count - 1; index >= 0; index--)
        {
            if (_segments[index].Count > 0)
            {
                return _segments[index][^1].Time;
            }
        }
        return null;
    }
}
