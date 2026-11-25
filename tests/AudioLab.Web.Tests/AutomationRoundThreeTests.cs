using System.Globalization;
using AudioLab.Core.Model;
using AudioLab.Web.Screens;
using AudioLab.Web.Services;
using AudioLab.Web.Session;

namespace AudioLab.Web.Tests;

/// <summary>Holds the tests of the marks a drag draws, the modes that cannot be chosen yet, the undo hold, and the announcements.</summary>
public sealed class AutomationRoundThreeTests
{
    private static readonly TrackId _track = TrackId.From("t1");

    private static readonly AutomationLanePlan _plan = new(AutomationParameter.Volume, TimelineGeometry.Default, 40);

    private static readonly IReadOnlyList<AutomationPoint> _points =
    [
        new AutomationPoint(1, 0.5),
        new AutomationPoint(2, 0.2),
        new AutomationPoint(3, 0.8),
    ];

    /// <summary>A moved drag changes the overlay to where the point is now, and a released one clears it.</summary>
    [Fact]
    public void AMovedDragChangesTheOverlayAndAReleasedOneClearsIt()
    {
        AutomationLanes lanes = new();
        AutomationDrag drag = new(_track, _points, 1, _plan, _plan.X(2), _plan.Y(0.2));
        lanes.Begin(drag);
        string before = AutomationMarks.Overlay(_plan, _points, lanes.Drag);

        drag.Move(_plan.X(2), _plan.Y(0.6));
        lanes.Moved();
        string moved = AutomationMarks.Overlay(_plan, _points, lanes.Drag);

        Assert.NotEqual(before, moved);
        Assert.Contains(_plan.Y(0.6).ToString("0.###", CultureInfo.InvariantCulture), moved);

        lanes.End();

        Assert.Equal(string.Empty, AutomationMarks.Overlay(_plan, _points, lanes.Drag));
    }

    /// <summary>The selected point's mark is where the point is, and nothing is marked with no selection.</summary>
    [Fact]
    public void TheSelectedMarkFollowsTheSelection()
    {
        string selected = AutomationMarks.Selected(_plan, _points, 1);

        Assert.StartsWith("M", selected);
        Assert.EndsWith("h0", selected);
        Assert.Equal(string.Empty, AutomationMarks.Selected(_plan, _points, null));
    }

    /// <summary>Off and read play the lanes; touch, latch and write cannot be chosen until the recorder is wired to playback.</summary>
    [Fact]
    public void TouchLatchAndWriteCannotBeChosenYet()
    {
        Assert.Null(AutomationModes.UnavailableReason(AutomationMode.Off));
        Assert.Null(AutomationModes.UnavailableReason(AutomationMode.Read));
        Assert.Equal(AutomationModes.FaderGrabPending, AutomationModes.UnavailableReason(AutomationMode.Touch));
        Assert.Equal(AutomationModes.FaderGrabPending, AutomationModes.UnavailableReason(AutomationMode.Latch));
        Assert.Equal(AutomationModes.FaderGrabPending, AutomationModes.UnavailableReason(AutomationMode.Write));
    }

    /// <summary>Undo and redo are held while a point is dragged, and free again when the drag ends.</summary>
    [Fact]
    public void UndoIsHeldWhileAPointIsDragged()
    {
        AutomationLanes lanes = new();
        Assert.False(lanes.HoldsHistory);

        lanes.Begin(new AutomationDrag(_track, _points, 1, _plan, _plan.X(2), _plan.Y(0.2)));
        Assert.True(lanes.HoldsHistory);

        lanes.End();
        Assert.False(lanes.HoldsHistory);
    }

    /// <summary>A move the server applied is announced as moved; a refused one announces nothing, since its reason is shown.</summary>
    [Fact]
    public void AnAppliedMoveIsAnnouncedAndARefusedOneIsNot()
    {
        string? applied = AutomationAnnouncement.MovedIfApplied(EditResult.Applied(true, []), AutomationParameter.Volume, 2, 0.6);
        string? refused = AutomationAnnouncement.MovedIfApplied(EditResult.Refused("No."), AutomationParameter.Volume, 2, 0.6);

        Assert.Equal("Point moved: volume 0.60 at 2.00 seconds.", applied);
        Assert.Null(refused);
    }

    /// <summary>The same words said twice are different announcements, so a screen reader reads the second one.</summary>
    [Fact]
    public void TheSameAnnouncementTwiceIsDifferentText()
    {
        AutomationLanes lanes = new();

        lanes.Announce("Point removed; 2 left in the lane.");
        string first = lanes.Announcement;
        lanes.Announce("Point removed; 2 left in the lane.");

        Assert.NotEqual(first, lanes.Announcement);
        Assert.Contains("Point removed; 2 left in the lane.", lanes.Announcement);
    }
}
