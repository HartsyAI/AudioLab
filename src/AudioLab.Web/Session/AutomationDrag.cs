using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Web.Session;

/// <summary>A point of an automation lane being dragged: it follows the pointer, and where it lands comes from the server's rule.</summary>
/// <remarks>
/// Nothing is sent while it moves. A press that never travels <see cref="Threshold"/> pixels is a click, and a release where the point
/// began sends nothing. The release sends the point's index as it was and absolute targets (D25), so a drag that comes back restores exactly.
/// </remarks>
public sealed class AutomationDrag
{
    /// <summary>How far the pointer travels, in pixels, before a press becomes a drag.</summary>
    public const double Threshold = 3;

    private readonly AutomationLanePlan _plan;

    private readonly IReadOnlyList<AutomationPoint> _points;

    private readonly AutomationPoint _origin;

    private readonly double _grabX;

    private readonly double _grabY;

    private readonly double _startX;

    private readonly double _startY;

    /// <summary>Starts a drag of the point at <paramref name="index"/>, pressed at a point in lane pixels.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The lane holds no point at that index.</exception>
    public AutomationDrag(TrackId track, IReadOnlyList<AutomationPoint> points, int index, AutomationLanePlan plan, double laneX, double laneY)
    {
        ArgumentNullException.ThrowIfNull(points);
        ArgumentNullException.ThrowIfNull(plan);
        if ((uint)index >= (uint)points.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, "The lane holds no point at that index.");
        }
        Track = track;
        Index = index;
        _points = points;
        _plan = plan;
        _origin = points[index];
        _startX = laneX;
        _startY = laneY;
        _grabX = laneX - plan.X(_origin.Time);
        _grabY = laneY - plan.Y(_origin.Value);
        Time = _origin.Time;
        Value = _origin.Value;
        Preview = points;
        PreviewIndex = index;
    }

    /// <summary>The track whose lane is dragged.</summary>
    public TrackId Track { get; }

    /// <summary>The index of the point as the lane held it when the drag began, which is the index the release sends.</summary>
    public int Index { get; }

    /// <summary>The lane as the lane held it when the drag began; a lane that is no longer this one cancels the drag.</summary>
    public IReadOnlyList<AutomationPoint> Source => _points;

    /// <summary>Whether the pointer has travelled far enough for this to be a drag and not a click.</summary>
    public bool Active { get; private set; }

    /// <summary>Where the point would go if released now, in seconds.</summary>
    public double Time { get; private set; }

    /// <summary>The value the point would take if released now.</summary>
    public double Value { get; private set; }

    /// <summary>The lane as the server's rule would leave it if released now.</summary>
    public IReadOnlyList<AutomationPoint> Preview { get; private set; }

    /// <summary>Where the dragged point would sit in <see cref="Preview"/>.</summary>
    public int PreviewIndex { get; private set; }

    /// <summary>Moves the pointer to a point in lane pixels.</summary>
    public void Move(double laneX, double laneY)
    {
        if (!Active && Math.Abs(laneX - _startX) < Threshold && Math.Abs(laneY - _startY) < Threshold)
        {
            return;
        }
        Active = true;
        Time = Math.Round(_plan.Time(laneX - _grabX), 3);
        Value = _plan.Value(laneY - _grabY);
        LaneEdit edit = AutomationRules.Move(_points, Index, new AutomationPoint(Time, Value));
        Preview = edit.Points;
        PreviewIndex = edit.Index;
    }

    /// <summary>Releases the point: the move to send, or null when it is a click or ended where it began.</summary>
    public AutomationMoveTarget? Commit()
    {
        if (!Active || (Math.Abs(Time - _origin.Time) < 1e-9 && Math.Abs(Value - _origin.Value) < 1e-9))
        {
            return null;
        }
        return new AutomationMoveTarget(Index, Time, Value, PreviewIndex);
    }
}

