using AudioLab.Core.Model;
using AudioLab.Core.Rules;
using AudioLab.Web.Components;
using AudioLab.Web.Interop;
using AudioLab.Web.Services;
using AudioLab.Web.Session;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace AudioLab.Web.Screens;

/// <summary>The automation lanes of the arrangement (D70): press to select or drag, double-click to add, keys to walk and edit.</summary>
public sealed partial class Timeline
{
    private readonly SemaphoreSlim _nudges = new(1, 1);

    [Inject]
    private AutomationLanes Lanes { get; set; } = default!;

    [Inject]
    private AutomationRecorder Recorder { get; set; } = default!;

    private AutomationLanePlan PlanOf(Track track) => new(track.View.AutomationParameter, Geometry, TrackLayout.AutomationHeight);

    // A pointer is dropped with the lane's drag when the document changed under it, so the drag is never sent from a stale lane.
    // Stopping the pointer runs no release, so a recorded drag releases its held value here, and the pass goes on without it.
    private void PruneAutomation()
    {
        AutomationDrag? held = Lanes.Drag;
        if (!Lanes.Prune(Document))
        {
            return;
        }
        Pointer.Stop();
        if (held is { Recorded: true })
        {
            Recorder.Release(held.Track, held.Value);
        }
    }

    // While a pass records a lane, that lane's points do not change: the pass writes them when it stops (D86).
    private bool RecordingBlocks(TrackId track)
    {
        if (!Recorder.Records(track))
        {
            return false;
        }
        Lanes.Announce("A pass is recording this lane; its points change when it stops.");
        return true;
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
            SelectPoint(track.Id, hit);
            return;
        }
        await AddPointAsync(track, plan.Time(x), plan.Value(y));
    }

    // Adds a point unless the lane already holds one at that time and value: an identical point is a zero-height step and a second undo step.
    private async Task AddPointAsync(Track track, double time, double value)
    {
        if (RecordingBlocks(track.Id))
        {
            return;
        }
        IReadOnlyList<AutomationPoint> lane = AutomationRules.Lane(track.Automation, track.View.AutomationParameter);
        if (lane.Any(point => Math.Abs(point.Time - time) < 1e-9 && Math.Abs(point.Value - value) < 1e-9))
        {
            return;
        }
        int index = AutomationRules.Insert(lane, new AutomationPoint(time, value)).Index;
        EditResult result = await Actions.Automation.Add(track.Id, track.View.AutomationParameter, time, value);
        if (result.Succeeded)
        {
            SelectPoint(track.Id, index);
            Lanes.Announce(AutomationAnnouncement.Added(track.View.AutomationParameter, time, value));
        }
    }

    // A press on a point selects it and, when the lane may be edited, may become a drag; nothing is sent until the release.
    private void AutomationPointDown((TrackId Track, PointerEventArgs Press) down)
    {
        // Only the primary button edits or grabs a lane, as a fader's press does (SliderInput).
        if (down.Press.Button != 0)
        {
            return;
        }
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
        AutomationParameter parameter = track.View.AutomationParameter;
        if (plan.IndexAt(lane, x, y) is not { } index)
        {
            GrabEmptyLane(track.Id, parameter, plan, y);
            return;
        }
        SelectPoint(track.Id, index);
        if (!Actions.CanEdit)
        {
            return;
        }
        TrackId owner = track.Id;
        // While a touch or latch pass records this control, the drag is the held control: it records and moves no point, and draws no landing.
        bool recorded = Recorder.RecordsLane(owner, parameter);
        if (!recorded && RecordingBlocks(owner))
        {
            return;
        }
        AutomationDrag drag = new(owner, lane, index, plan, x, y) { Recorded = recorded };
        Lanes.Begin(drag);
        if (recorded)
        {
            Recorder.Grab(owner, parameter, drag.Value);
            Lanes.Announce("The held value is recorded into the pass; the point does not move.");
        }
        // The surface scrolls under the pointer, so where the pointer is in the lane is read again on every move and on the release.
        Pointer.Begin(
            (px, py) =>
            {
                if (LaneCoordinates(owner, px, py, out double moveX, out double moveY))
                {
                    drag.Move(moveX, moveY);
                    if (recorded)
                    {
                        Recorder.Move(owner, drag.Value);
                    }
                    Lanes.Moved();
                }
            },
            (px, py) =>
            {
                if (LaneCoordinates(owner, px, py, out double endX, out double endY))
                {
                    drag.Move(endX, endY);
                }
                _ = InvokeAsync(() => AutomationReleaseAsync(drag, recorded));
            },
            () =>
            {
                Lanes.End();
                if (recorded)
                {
                    // Escape or a lost pointer drops the pass as well as the drag: nothing it recorded is committed.
                    Recorder.Drop();
                }
            });
    }

    // A press on an empty part of a lane that a touch or latch pass records grabs the lane at the value under the pointer, and moves no point (D86).
    private void GrabEmptyLane(TrackId track, AutomationParameter parameter, AutomationLanePlan plan, double pressY)
    {
        if (!Actions.CanEdit || !Recorder.Grab(track, parameter, plan.Value(pressY)))
        {
            return;
        }
        Lanes.Announce("The held value is recorded into the pass; no point is added.");
        Pointer.Begin(
            (px, py) =>
            {
                if (LaneCoordinates(track, px, py, out _, out double moveY))
                {
                    Recorder.Move(track, plan.Value(moveY));
                }
            },
            (px, py) =>
            {
                double endY = LaneCoordinates(track, px, py, out _, out double releaseY) ? releaseY : pressY;
                _ = InvokeAsync(() => Recorder.Release(track, plan.Value(endY)));
            },
            () =>
            {
                // Escape or a lost pointer drops the pass that the grab fed, as it does for a point drag.
                if (Recorder.RecordsLane(track, parameter))
                {
                    Recorder.Drop();
                }
            });
    }

    // The release sends the point as one gesture with absolute targets; the lane keeps the drag's landing until the server has answered.
    // A drag that was recorded releases its segment instead, and sends no point move.
    private async Task AutomationReleaseAsync(AutomationDrag drag, bool recorded)
    {
        if (recorded)
        {
            Recorder.Release(drag.Track, drag.Value);
            Lanes.End();
            return;
        }
        // A drag begun before the pass started and let go during it changes no point: the pass owns the lane until it stops.
        if (RecordingBlocks(drag.Track))
        {
            Lanes.End();
            return;
        }
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
            if (AutomationAnnouncement.MovedIfApplied(result, parameter, send.Time, send.Value) is { } moved)
            {
                SelectPoint(drag.Track, send.LandsAt);
                Lanes.Announce(moved);
            }
        }
        finally
        {
            Lanes.End();
        }
    }

    // The keys of a lane: the decision is AutomationKeys, and this runs it against the document as it is now.
    private async Task AutomationKeyAsync((TrackId Track, KeyboardEventArgs Key) press)
    {
        Track? track = CurrentTrack(press.Track);
        if (!Actions.CanEdit || track is null || !track.View.AutomationVisible)
        {
            return;
        }
        KeyboardEventArgs key = press.Key;
        KeyChord chord = new(key.Key, key.CtrlKey || key.MetaKey, key.ShiftKey, key.AltKey, key.Repeat);
        AutomationParameter parameter = track.View.AutomationParameter;
        IReadOnlyList<AutomationPoint> lane = AutomationRules.Lane(track.Automation, parameter);
        int? selected = Lanes.Track == track.Id ? Lanes.Index : null;
        AutomationKeyAction action = AutomationKeys.Decide(chord, lane.Count, selected);
        switch (action.Kind)
        {
            case AutomationKeyKind.Select:
                SelectPoint(track.Id, action.Index);
                break;
            case AutomationKeyKind.Add when action.Index is { } at:
                // Enter adds after the selected point, one grid step later and at its value.
                await AddPointAsync(track, lane[at].Time + new GridSnap(Document).Step(lane[at].Time), lane[at].Value);
                break;
            case AutomationKeyKind.Delete when action.Index is { } index:
                await DeletePointAsync(track, parameter, index);
                break;
            case AutomationKeyKind.Nudge when action.Index is not null && action.Direction is { } direction:
                await NudgeAsync(track.Id, direction);
                break;
        }
    }

    private async Task DeletePointAsync(Track track, AutomationParameter parameter, int index)
    {
        if (RecordingBlocks(track.Id))
        {
            return;
        }
        EditResult result = await Actions.Automation.Delete(track.Id, parameter, index);
        if (!result.Succeeded)
        {
            return;
        }
        int remaining = AutomationRules.Lane(CurrentTrack(track.Id)?.Automation ?? track.Automation, parameter).Count;
        SelectPoint(track.Id, remaining == 0 ? null : Math.Min(index, remaining - 1));
        Lanes.Announce(AutomationAnnouncement.Removed(remaining));
    }

    // Nudges run one at a time, and each one reads the lane after the reply to the one before it, so two quick presses move the same point twice.
    private async Task NudgeAsync(TrackId track, NudgeDirection direction)
    {
        await _nudges.WaitAsync();
        try
        {
            if (RecordingBlocks(track))
            {
                return;
            }
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
            if (AutomationAnnouncement.MovedIfApplied(result, current.View.AutomationParameter, send.Time, send.Value) is { } moved)
            {
                SelectPoint(track, send.LandsAt);
                Lanes.Announce(moved);
            }
        }
        finally
        {
            _nudges.Release();
        }
    }

    // Selects a point and announces it, so a selection by key, by press or by an edit is heard the same way.
    private void SelectPoint(TrackId track, int? index)
    {
        Lanes.Select(track, index);
        if (index is not { } at || CurrentTrack(track) is not { } owner)
        {
            return;
        }
        IReadOnlyList<AutomationPoint> lane = AutomationRules.Lane(owner.Automation, owner.View.AutomationParameter);
        if (at < lane.Count)
        {
            Lanes.Announce(AutomationAnnouncement.Selected(owner.View.AutomationParameter, at, lane.Count, lane[at].Time, lane[at].Value));
        }
    }

    private Task ShowAutomationAsync(TrackId track) => Actions.Automation.SetView(track, true, null);

    // The track as the document holds it now: after a reply the live document is already the new one, before the screen has drawn it.
    private Track? CurrentTrack(TrackId track)
    {
        ProjectDocument document = Actions.Automation.Current() ?? Document;
        return document.Tracks.FirstOrDefault(candidate => candidate.Id == track);
    }

    private int RowOf(TrackId track) => Document.Tracks.ToList().FindIndex(candidate => candidate.Id == track);
}
