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

    /// <summary>Whether the event was gone from the clip when it was released: 1 then, else 0.</summary>
    public int Lost { get; private set; }

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
    public RollEdit? Commit(MidiClip current)
    {
        ArgumentNullException.ThrowIfNull(current);
        // The event is found again by its time in the clip as it is now; gone, there is nothing to move.
        MidiControlChange? moved = null;
        bool taken = false;
        foreach (MidiControlChange other in current.ControlChanges.Where(other => other.Controller == _event.Controller))
        {
            if (Math.Abs(other.Time - _event.Time) < TimelineRules.NoMove)
            {
                moved = other;
            }
            else if (Math.Abs(other.Time - Time) < TimelineRules.NoMove)
            {
                taken = true;
            }
        }
        Lost = moved is null ? 1 : 0;
        if (!Active || Math.Abs(Time - _event.Time) < TimelineRules.NoMove || taken || moved is not { } change)
        {
            return null;
        }
        ClipId clip = current.Id;
        double time = Time;
        return new RollEdit(project =>
        [
            new RemoveControlChanges { Project = project, Clip = clip, Controller = change.Controller, Time = change.Time },
            new AddControlChange { Project = project, Clip = clip, Controller = change.Controller, Time = time, Value = change.Value },
        ]);
    }
}
