using AudioLab.Core.Model;
using AudioLab.Core.Rules;
using AudioLab.Web.Session;

namespace AudioLab.Web.Screens;

/// <summary>What is selected in the automation lanes and what is being dragged: this client's view state, never part of the document.</summary>
public sealed class AutomationLanes
{
    /// <summary>Raised when the selection or the drag changed.</summary>
    public event Action? Changed;

    /// <summary>The track whose point is selected, or null.</summary>
    public TrackId? Track { get; private set; }

    /// <summary>The index of the selected point, or null.</summary>
    public int? Index { get; private set; }

    /// <summary>The point being dragged, or null. It stays after the release until the server has answered.</summary>
    public AutomationDrag? Drag { get; private set; }

    /// <summary>Selects a point of a track's lane, or nothing when <paramref name="index"/> is null.</summary>
    public void Select(TrackId track, int? index)
    {
        if (Track == track && Index == index)
        {
            return;
        }
        Track = index is null ? null : track;
        Index = index;
        Changed?.Invoke();
    }

    /// <summary>Begins a drag of a point.</summary>
    public void Begin(AutomationDrag drag)
    {
        ArgumentNullException.ThrowIfNull(drag);
        Drag = drag;
        Changed?.Invoke();
    }

    /// <summary>Says the drag moved, so the lane draws its new place.</summary>
    public void Moved() => Changed?.Invoke();

    /// <summary>Ends the drag, so the lane draws its own points again.</summary>
    public void End()
    {
        if (Drag is null)
        {
            return;
        }
        Drag = null;
        Changed?.Invoke();
    }

    /// <summary>Drops a selection of a point the document no longer has, and a drag whose lane another client changed.</summary>
    /// <returns>True when a drag was in progress and has been dropped, so the caller can let go of the pointer.</returns>
    public bool Prune(ProjectDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        bool dropped = false;
        if (Drag is { } drag)
        {
            Track? track = document.Tracks.FirstOrDefault(candidate => candidate.Id == drag.Track);
            if (track is null || !ReferenceEquals(AutomationRules.Lane(track.Automation, track.View.AutomationParameter), drag.Source))
            {
                Drag = null;
                dropped = true;
            }
        }
        if (Track is { } owner && Index is { } index)
        {
            Track? track = document.Tracks.FirstOrDefault(candidate => candidate.Id == owner);
            if (track is null || index >= AutomationRules.Lane(track.Automation, track.View.AutomationParameter).Count)
            {
                Track = null;
                Index = null;
            }
        }
        if (dropped || Track is null)
        {
            Changed?.Invoke();
        }
        return dropped;
    }
}
