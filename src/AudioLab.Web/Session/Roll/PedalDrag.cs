using AudioLab.Core.Commands;
using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Web.Session.Roll;

/// <summary>A drag on a pedal event: it moves along the grid and keeps its value; an event is found by its time, so a move is two commands.</summary>
public sealed class PedalDrag : IRollGesture
{
    private readonly MidiClip _clip;

    private readonly RollGeometry _geometry;

    private readonly GridSnap _grid;

    private readonly MidiControlChange _event;

    private readonly double _startX;

    /// <summary>Starts a drag of an event, pressed at <paramref name="x"/> pixels from the left of the lane.</summary>
    public PedalDrag(ProjectDocument document, MidiClip clip, RollGeometry geometry, MidiControlChange change, double x)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(clip);
        _clip = clip;
        _geometry = geometry;
        _grid = new GridSnap(document);
        _event = change;
        _startX = x;
        Time = change.Time;
    }

    /// <summary>Where the event would be if released now, in seconds from the clip start.</summary>
    public double Time { get; private set; }

    /// <inheritdoc />
    public bool Active { get; private set; }

    /// <inheritdoc />
    public void Update(double x, double y)
    {
        if (!Active && Math.Abs(x - _startX) < NoteDrag.Threshold)
        {
            return;
        }
        Active = true;
        double wanted = _event.Time + _geometry.Seconds(x - _startX);
        Time = Math.Clamp(_grid.Snap(_clip.Start + wanted) - _clip.Start, 0, _clip.Length);
    }

    /// <inheritdoc />
    public RollEdit? Commit()
    {
        bool taken = _clip.ControlChanges.Any(
            other => other.Controller == _event.Controller && other.Time != _event.Time && Math.Abs(other.Time - Time) < TimelineRules.NoMove);
        if (!Active || Math.Abs(Time - _event.Time) < TimelineRules.NoMove || taken)
        {
            return null;
        }
        ClipId clip = _clip.Id;
        MidiControlChange moved = _event;
        double time = Time;
        return new RollEdit(project =>
        [
            new RemoveControlChanges { Project = project, Clip = clip, Controller = moved.Controller, Time = moved.Time },
            new AddControlChange { Project = project, Clip = clip, Controller = moved.Controller, Time = time, Value = moved.Value },
        ]);
    }
}
