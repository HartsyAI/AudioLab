using AudioLab.Core.Model;
using AudioLab.Core.Rules;
using AudioLab.Web.Components;
using AudioLab.Web.Services;
using AudioLab.Web.Session;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace AudioLab.Web.Screens;

/// <summary>The automation lanes of the arrangement (D69): a press selects or drags a point, a double-click adds one, and the keys walk and edit.</summary>
public sealed partial class Timeline
{
    private readonly SemaphoreSlim _nudges = new(1, 1);

    [Inject]
    private AutomationLanes Lanes { get; set; } = default!;

    private AutomationLanePlan PlanOf(Track track) => new(track.View.AutomationParameter, Geometry, TrackLayout.AutomationHeight);

    // A pointer is dropped with the lane's drag when the document changed under it, so the drag is never sent from a stale lane.
    private void PruneAutomation()
    {
        if (Lanes.Prune(Document))
        {
            Pointer.Stop();
        }
    }

    // The lane's own pixels: across from the left of the lanes, and down from the top of the lane.
    private bool LaneCoordinates(TrackId track, double clientX, double clientY, out double laneX, out double laneY)
    {
        laneX = 0;
        laneY = 0;
        int row = RowOf(track);
        (double Left, double Top)? origin = Metrics.Origin(LaneSurfaceId);
        if (origin is not { } at || row < 0)
        {
            return false;
        }
        laneX = clientX - at.Left;
        laneY = clientY - at.Top - Layout.AutomationTop(row);
        return true;
    }

    // A double-click on an empty part of the lane adds a point at the time and value under the pointer; on a point it selects that point.
    private async Task AutomationAddAsync((TrackId Track, MouseEventArgs Click) click)
    {
        Track? track = CurrentTrack(click.Track);
        if (!Actions.CanEdit || track is null || !track.View.AutomationVisible)
        {
            return;
        }
        if (!LaneCoordinates(track.Id, click.Click.ClientX, click.Click.ClientY, out double x, out double y))
        {
            return;
        }
        AutomationLanePlan plan = PlanOf(track);
        IReadOnlyList<AutomationPoint> lane = AutomationRules.Lane(track.Automation, track.View.AutomationParameter);
        if (plan.IndexAt(lane, x, y) is { } hit)
        {
            Lanes.Select(track.Id, hit);
            return;
        }
        await AddPointAsync(track, plan.Time(x), plan.Value(y));
    }

    // Adds a point unless the lane already holds one at that time and value: an identical point is a zero-height step and a second undo step.
    private async Task AddPointAsync(Track track, double time, double value)
    {
        IReadOnlyList<AutomationPoint> lane = AutomationRules.Lane(track.Automation, track.View.AutomationParameter);
        if (lane.Any(point => Math.Abs(point.Time - time) < 1e-9 && Math.Abs(point.Value - value) < 1e-9))
        {
            return;
        }
        int index = AutomationRules.Insert(lane, new AutomationPoint(time, value)).Index;
        EditResult result = await Actions.Automation.Add(track.Id, track.View.AutomationParameter, time, value);
        if (result.Succeeded)
        {
            Lanes.Select(track.Id, index);
        }
    }

    // A press on a point selects it and, when the lane may be edited, may become a drag; nothing is sent until the release.
    private void AutomationPointDown((TrackId Track, PointerEventArgs Press) down)
    {
        Track? track = CurrentTrack(down.Track);
        if (track is null || !track.View.AutomationVisible)
        {
            return;
        }
        if (!LaneCoordinates(track.Id, down.Press.ClientX, down.Press.ClientY, out double x, out double y))
        {
            return;
        }
        AutomationLanePlan plan = PlanOf(track);
        IReadOnlyList<AutomationPoint> lane = AutomationRules.Lane(track.Automation, track.View.AutomationParameter);
        if (plan.IndexAt(lane, x, y) is not { } index)
        {
            return;
        }
        Lanes.Select(track.Id, index);
        if (!Actions.CanEdit)
        {
            return;
        }
        TrackId owner = track.Id;
        AutomationDrag drag = new(owner, lane, index, plan, x, y);
        Lanes.Begin(drag);
        // The surface scrolls under the pointer, so where the pointer is in the lane is read again on every move and on the release.
        Pointer.Begin(
            (px, py) =>
            {
                if (LaneCoordinates(owner, px, py, out double moveX, out double moveY))
                {
                    drag.Move(moveX, moveY);
                    Lanes.Moved();
                }
            },
            (px, py) =>
            {
                if (LaneCoordinates(owner, px, py, out double endX, out double endY))
                {
                    drag.Move(endX, endY);
                }
                _ = InvokeAsync(() => AutomationReleaseAsync(drag));
            },
            () => Lanes.End());
    }

    // The release sends the point as one gesture with absolute targets; the lane keeps the drag's landing until the server has answered.
    private async Task AutomationReleaseAsync(AutomationDrag drag)
    {
        AutomationMoveTarget? target = drag.Commit();
        if (target is not { } send)
        {
            Lanes.End();
            return;
        }
        if (!Actions.CanEdit)
        {
            Toasts.Show(ToastKind.Warning, "The connection to the server was lost during the drag, so the point was not moved.");
            Lanes.End();
            return;
        }
        try
        {
            AutomationParameter parameter = CurrentTrack(drag.Track)?.View.AutomationParameter ?? AutomationParameter.Volume;
            EditResult result = await Actions.Automation.Move(drag.Track, parameter, send);
            if (result.Succeeded)
            {
                Lanes.Select(drag.Track, send.LandsAt);
            }
        }
        finally
        {
            Lanes.End();
        }
    }

    // The keys of a lane: the selected point is walked, added, moved and removed. Escape cancels a drag, which the pointer does.
    private async Task AutomationKeyAsync((TrackId Track, KeyboardEventArgs Key) press)
    {
        Track? track = CurrentTrack(press.Track);
        if (!Actions.CanEdit || track is null || !track.View.AutomationVisible)
        {
            return;
        }
        AutomationParameter parameter = track.View.AutomationParameter;
        IReadOnlyList<AutomationPoint> lane = AutomationRules.Lane(track.Automation, parameter);
        int? selected = Lanes.Track == track.Id ? Lanes.Index : null;
        KeyboardEventArgs key = press.Key;
        if (Shortcuts.AutomationPrevious.Matches(key))
        {
            Lanes.Select(track.Id, lane.Count == 0 ? null : Math.Max(0, (selected ?? lane.Count) - 1));
        }
        else if (Shortcuts.AutomationNext.Matches(key))
        {
            Lanes.Select(track.Id, lane.Count == 0 ? null : Math.Min(lane.Count - 1, (selected ?? -1) + 1));
        }
        else if (Shortcuts.AutomationAdd.Matches(key))
        {
            // Enter adds after the selected point, one grid step later and at its value; with nothing selected it adds nothing.
            if (!key.Repeat && selected is { } at && at < lane.Count)
            {
                await AddPointAsync(track, lane[at].Time + new GridSnap(Document).Step(lane[at].Time), lane[at].Value);
            }
        }
        else if (Shortcuts.AutomationDelete.Matches(key) && selected is { } index && index < lane.Count)
        {
            EditResult result = await Actions.Automation.Delete(track.Id, parameter, index);
            if (result.Succeeded)
            {
                int remaining = lane.Count - 1;
                Lanes.Select(track.Id, remaining == 0 ? null : Math.Min(index, remaining - 1));
            }
        }
        else if (Nudge(key) is { } direction && selected is not null)
        {
            await NudgeAsync(track.Id, direction);
        }
    }

    // Nudges run one at a time, and each one reads the lane after the reply to the one before it, so two quick presses move the same point twice.
    private async Task NudgeAsync(TrackId track, NudgeDirection direction)
    {
        await _nudges.WaitAsync();
        try
        {
            Track? current = CurrentTrack(track);
            if (current is null || !current.View.AutomationVisible || Lanes.Track != track || Lanes.Index is not { } index)
            {
                return;
            }
            IReadOnlyList<AutomationPoint> lane = AutomationRules.Lane(current.Automation, current.View.AutomationParameter);
            if (index >= lane.Count)
            {
                return;
            }
            double timeStep = new GridSnap(Document).Step(lane[index].Time);
            AutomationMoveTarget? target = AutomationNudge.Target(lane, index, current.View.AutomationParameter, direction, timeStep);
            if (target is not { } send)
            {
                return;
            }
            EditResult result = await Actions.Automation.Move(track, current.View.AutomationParameter, send);
            if (result.Succeeded)
            {
                Lanes.Select(track, send.LandsAt);
            }
        }
        finally
        {
            _nudges.Release();
        }
    }

    private Task ShowAutomationAsync(TrackId track) => Actions.Automation.SetView(track, true, null);

    private static NudgeDirection? Nudge(KeyboardEventArgs key)
    {
        if (Shortcuts.AutomationEarlier.Matches(key))
        {
            return NudgeDirection.Earlier;
        }
        if (Shortcuts.AutomationLater.Matches(key))
        {
            return NudgeDirection.Later;
        }
        if (Shortcuts.AutomationHigher.Matches(key))
        {
            return NudgeDirection.Higher;
        }
        return Shortcuts.AutomationLower.Matches(key) ? NudgeDirection.Lower : null;
    }

    // The track as the document holds it now: after a reply the live document is already the new one, before the screen has drawn it.
    private Track? CurrentTrack(TrackId track)
    {
        ProjectDocument document = Actions.Automation.Current() ?? Document;
        return document.Tracks.FirstOrDefault(candidate => candidate.Id == track);
    }

    private int RowOf(TrackId track) => Document.Tracks.ToList().FindIndex(candidate => candidate.Id == track);
}
