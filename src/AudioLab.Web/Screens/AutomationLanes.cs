using AudioLab.Core.Model;
using AudioLab.Core.Rules;
using AudioLab.Web.Session;

namespace AudioLab.Web.Screens;

/// <summary>What is selected in the automation lanes and what is being dragged: this client's view state, never part of the document.</summary>
/// <remarks>Each change names the track whose lane it touches, so only that lane redraws (D68).</remarks>
public sealed class AutomationLanes
{
    /// <summary>Raised with the track whose lane changed.</summary>
    public event Action<TrackId>? Changed;

    /// <summary>Raised when the words for the status region changed.</summary>
    public event Action? AnnouncementChanged;

    private int _announcements;

    /// <summary>The last announcement, read by the status region of the arrangement (D68).</summary>
    public string Announcement { get; private set; } = string.Empty;

    /// <summary>The track whose point is selected, or null.</summary>
    public TrackId? Track { get; private set; }

    /// <summary>The index of the selected point, or null.</summary>
    public int? Index { get; private set; }

    /// <summary>The point being dragged, or null. It stays after the release until the server has answered.</summary>
    public AutomationDrag? Drag { get; private set; }

    /// <summary>Whether a point is being dragged, which holds undo and redo back until the drag ends (as a clip drag does).</summary>
    public bool HoldsHistory => Drag is not null;

    /// <summary>Selects a point of a track's lane, or nothing when <paramref name="index"/> is null.</summary>
    public void Select(TrackId track, int? index)
    {
        TrackId? before = Track;
        int? beforeIndex = Index;
        Track = index is null ? null : track;
        Index = index;
        if (Track == before && Index == beforeIndex)
        {
            return;
        }
        if (before is { } old)
        {
            Changed?.Invoke(old);
        }
        if (Track is { } now && now != before)
        {
            Changed?.Invoke(now);
        }
    }

    /// <summary>Says a sentence in the status region, so that it is read to the user.</summary>
    public void Announce(string text)
    {
        // A screen reader reads a change of text only, so each announcement carries a count and the same words are read again.
        _announcements++;
        Announcement = $"{text} ({_announcements})";
        AnnouncementChanged?.Invoke();
    }

    /// <summary>Begins a drag of a point.</summary>
    public void Begin(AutomationDrag drag)
    {
        ArgumentNullException.ThrowIfNull(drag);
        Drag = drag;
        Changed?.Invoke(drag.Track);
    }

    /// <summary>Says the drag moved, so its lane draws the point's new place.</summary>
    public void Moved()
    {
        if (Drag is { } drag)
        {
            Changed?.Invoke(drag.Track);
        }
    }

    /// <summary>Ends the drag, so the lane draws its own points again.</summary>
    public void End()
    {
        if (Drag is not { } drag)
        {
            return;
        }
        Drag = null;
        Changed?.Invoke(drag.Track);
    }

    /// <summary>Drops a selection of a point the document no longer has, and a drag whose lane another client changed.</summary>
    /// <remarks>Raises a change only for what it drops, so a document that changed nothing here costs no redraw.</remarks>
    /// <returns>True when a drag was in progress and has been dropped, so the caller can let go of the pointer.</returns>
    public bool Prune(ProjectDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        bool dropped = false;
        if (Drag is { } drag && !ReferenceEquals(LaneOf(document, drag.Track), drag.Source))
        {
            Drag = null;
            dropped = true;
            Changed?.Invoke(drag.Track);
        }
        if (Track is { } owner && Index is { } index && index >= (LaneOf(document, owner)?.Count ?? 0))
        {
            Track = null;
            Index = null;
            Changed?.Invoke(owner);
        }
        return dropped;
    }

    private static IReadOnlyList<AutomationPoint>? LaneOf(ProjectDocument document, TrackId track)
    {
        return document.Tracks.FirstOrDefault(candidate => candidate.Id == track) is { } found
            ? AutomationRules.Lane(found.Automation, found.View.AutomationParameter)
            : null;
    }
}
