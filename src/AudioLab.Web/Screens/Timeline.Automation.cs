using AudioLab.Core.Model;
using AudioLab.Core.Rules;
using AudioLab.Web.Components;
using AudioLab.Web.Services;
using AudioLab.Web.Session;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace AudioLab.Web.Screens;

/// <summary>The automation lanes of the arrangement (D69): a double-click adds a point, a drag moves one, and the keys walk and edit the selected point.</summary>
public sealed partial class Timeline
{
    [Inject]
    private AutomationLanes Lanes { get; set; } = default!;

    // Every automation key moves a point by a fiftieth of its lane's range, or by a grid step in time.
    private const double ValueSteps = 50;

    private AutomationLanePlan PlanOf(Track track) => new(track.View.AutomationParameter, Geometry, TrackLayout.AutomationHeight);

    private void OnLanesChanged()
    {
        _ = InvokeAsync(StateHasChanged);
    }

    // A pointer is dropped with the lane's drag when the document changed under it, so the drag is never sent from a stale lane.
    private void PruneAutomation()
    {
        if (Lanes.Prune(Document))
        {
            Pointer.Stop();
        }
    }

    // A double-click on an empty part of the lane adds a point at the time and value under the pointer.
    private async Task AutomationAddAsync((TrackId Track, double X, double Y) click)
    {
        if (!Actions.CanEdit || TrackById(click.Track) is not { View.AutomationVisible: true } track)
        {
            return;
        }
        AutomationLanePlan plan = PlanOf(track);
        await AddPointAsync(track, AutomationRules.Lane(track.Automation, track.View.AutomationParameter), plan.Time(click.X), plan.Value(click.Y));
    }

    private async Task AddPointAsync(Track track, IReadOnlyList<AutomationPoint> lane, double time, double value)
    {
        int index = AutomationRules.Insert(lane, new AutomationPoint(time, value)).Index;
        EditResult result = await Actions.Automation.Add(track.Id, track.View.AutomationParameter, time, value);
        if (result.Succeeded)
        {
            Lanes.Select(track.Id, index);
        }
    }

    // A press on a point selects it and, when the lane may be edited, may become a drag; nothing is sent until the release.
    private void AutomationPointDown((TrackId Track, int Index, PointerEventArgs Press) down)
    {
        Lanes.Select(down.Track, down.Index);
        (double Left, double Top)? origin = Metrics.Origin(LaneSurfaceId);
        if (!Actions.CanEdit || origin is not { } at || TrackById(down.Track) is not { View.AutomationVisible: true } track)
        {
            return;
        }
        IReadOnlyList<AutomationPoint> lane = AutomationRules.Lane(track.Automation, track.View.AutomationParameter);
        if (down.Index >= lane.Count)
        {
            return;
        }
        double laneTop = Layout.AutomationTop(RowOf(track.Id));
        AutomationDrag drag = new(track.Id, lane, down.Index, PlanOf(track), down.Press.ClientX - at.Left, down.Press.ClientY - at.Top - laneTop);
        Lanes.Begin(drag);
        // The surface scrolls under the pointer, so where the pointer is in the lane is read again on every move and on the release.
        Pointer.Begin(
            (x, y) =>
            {
                if (Metrics.Origin(LaneSurfaceId) is { } now)
                {
                    drag.Move(x - now.Left, y - now.Top - laneTop);
                    Lanes.Moved();
                }
            },
            (x, y) =>
            {
                if (Metrics.Origin(LaneSurfaceId) is { } now)
                {
                    drag.Move(x - now.Left, y - now.Top - laneTop);
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
            EditResult result = await Actions.Automation.Move(drag.Track, DrawnParameter(drag.Track), send);
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
        if (!Actions.CanEdit || TrackById(press.Track) is not { View.AutomationVisible: true } track)
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
            double time = selected is { } at ? lane[at].Time : 0;
            double value = AutomationRules.ValueAt(lane, time) ?? AutomationRules.Identity(parameter);
            await AddPointAsync(track, lane, time, value);
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
        else if (selected is { } moved && moved < lane.Count)
        {
            await NudgeAsync(track, parameter, lane, moved, key);
        }
    }

    // Alt with an arrow moves the selected point: earlier or later by a grid step, higher or lower by a fiftieth of the lane's range.
    private async Task NudgeAsync(Track track, AutomationParameter parameter, IReadOnlyList<AutomationPoint> lane, int index, KeyboardEventArgs key)
    {
        AutomationPoint point = lane[index];
        double range = AutomationRules.Max(parameter) - AutomationRules.Min(parameter);
        double time = point.Time;
        double value = point.Value;
        if (Shortcuts.AutomationEarlier.Matches(key))
        {
            time = Math.Max(0, point.Time - new GridSnap(Document).Step(point.Time));
        }
        else if (Shortcuts.AutomationLater.Matches(key))
        {
            time = Math.Min(ProjectLimits.MaxSeconds, point.Time + new GridSnap(Document).Step(point.Time));
        }
        else if (Shortcuts.AutomationHigher.Matches(key))
        {
            value = AutomationRules.Clamp(parameter, point.Value + (range / ValueSteps));
        }
        else if (Shortcuts.AutomationLower.Matches(key))
        {
            value = AutomationRules.Clamp(parameter, point.Value - (range / ValueSteps));
        }
        else
        {
            return;
        }
        if (Math.Abs(time - point.Time) < 1e-9 && Math.Abs(value - point.Value) < 1e-9)
        {
            return;
        }
        LaneEdit edit = AutomationRules.Move(lane, index, new AutomationPoint(time, value));
        EditResult result = await Actions.Automation.Move(track.Id, parameter, new AutomationMoveTarget(index, time, value, edit.Index));
        if (result.Succeeded)
        {
            Lanes.Select(track.Id, edit.Index);
        }
    }

    private Track? TrackById(TrackId track) => Document.Tracks.FirstOrDefault(candidate => candidate.Id == track);

    private int RowOf(TrackId track) => Document.Tracks.ToList().FindIndex(candidate => candidate.Id == track);

    private AutomationParameter DrawnParameter(TrackId track) => TrackById(track)?.View.AutomationParameter ?? AutomationParameter.Volume;
}
