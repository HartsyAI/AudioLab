using AudioLab.Core.Commands;
using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Web.Session;

/// <summary>Records one pass of a track's automation while the transport plays (D25), fed by the transport's ticks and the control's values.</summary>
/// <remarks>
/// Write records every sample from the start of the pass to its stop. Touch records only while the control is held and hands the lane back to
/// the old curve after the release. Latch records from the first touch to the stop and keeps the value. Nothing is recorded in Read or Off.
/// The points are sent as they were written; the server folds them into the lane (<c>automation_write_commit</c>, D25).
/// </remarks>
public sealed class AutomationWriter(AutomationMode mode, AutomationParameter parameter)
{
    private readonly List<WrittenPoint> _points = [];

    private bool _passing;

    private bool _held;

    private bool _latched;

    /// <summary>The mode the pass records in.</summary>
    public AutomationMode Mode { get; } = mode is AutomationMode.Write or AutomationMode.Touch or AutomationMode.Latch
        ? mode
        : throw new ArgumentOutOfRangeException(nameof(mode), mode, "Only write, touch and latch record a pass.");

    /// <summary>The parameter the pass writes.</summary>
    public AutomationParameter Parameter { get; } = parameter;

    /// <summary>Whether the pass has begun and has not stopped.</summary>
    public bool Passing => _passing;

    /// <summary>Whether the next sample is recorded: the pass is running and, for touch and latch, the control has been touched.</summary>
    public bool Recording => _passing && (Mode == AutomationMode.Write || _held || _latched);

    /// <summary>Whether the pass hit the most points a lane may hold, after which further samples are not kept.</summary>
    public bool Full => _points.Count >= ProjectLimits.MaxListItems;

    /// <summary>The points written so far, in the order the ticks gave them.</summary>
    public IReadOnlyList<WrittenPoint> Points => _points;

    /// <summary>Starts the pass when the transport starts playing, at a time in seconds with the control's value then.</summary>
    public void Begin(double time, double value)
    {
        Reset();
        _passing = true;
        if (Mode == AutomationMode.Write)
        {
            Record(time, value);
        }
    }

    /// <summary>The control is touched at a time with a value; for touch and latch the pass starts recording here.</summary>
    public void Grab(double time, double value)
    {
        if (!_passing || Mode == AutomationMode.Write)
        {
            return;
        }
        _held = true;
        _latched = Mode == AutomationMode.Latch;
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

    /// <summary>A sample of the control's value at a time, from a transport tick; recorded only while the pass records.</summary>
    public void Sample(double time, double value)
    {
        if (Recording)
        {
            Record(time, value);
        }
    }

    /// <summary>Stops the pass at a time, and returns what it recorded: the points to commit, or null when nothing was written.</summary>
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
        return _points.Count == 0 ? null : new AutomationPass([.. _points], Mode == AutomationMode.Touch);
    }

    private void Record(double time, double value)
    {
        if (Full)
        {
            return;
        }
        double at = Math.Clamp(time, 0, ProjectLimits.MaxSeconds);
        _points.Add(new WrittenPoint(at, AutomationRules.Clamp(Parameter, value)));
    }

    private void Reset()
    {
        _points.Clear();
        _held = false;
        _latched = false;
    }
}
