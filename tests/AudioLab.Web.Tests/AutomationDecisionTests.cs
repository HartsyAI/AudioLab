using AudioLab.Core.Model;
using AudioLab.Web.Interop;
using AudioLab.Web.Session;

namespace AudioLab.Web.Tests;

/// <summary>Holds the decisions of an automation lane: its keys, the redraw gate, the pass checks and the announcements (D68, D69).</summary>
public sealed class AutomationDecisionTests
{
    private static readonly KeyChord _right = new("ArrowRight", false, false, false);

    private static readonly KeyChord _left = new("ArrowLeft", false, false, false);

    private static readonly KeyChord _enter = new("Enter", false, false, false);

    private static readonly KeyChord _delete = new("Delete", false, false, false);

    /// <summary>Right with nothing selected selects the first point; left selects the last.</summary>
    [Fact]
    public void ArrowsWithNothingSelectedStartAtTheEnds()
    {
        Assert.Equal(new AutomationKeyAction(AutomationKeyKind.Select, 0), AutomationKeys.Decide(_right, 3, null));
        Assert.Equal(new AutomationKeyAction(AutomationKeyKind.Select, 2), AutomationKeys.Decide(_left, 3, null));
    }

    /// <summary>The arrows stop at the first and the last point, and select nothing in a lane with no points.</summary>
    [Fact]
    public void ArrowsStopAtTheEndsOfTheLane()
    {
        Assert.Equal(new AutomationKeyAction(AutomationKeyKind.Select, 2), AutomationKeys.Decide(_right, 3, 2));
        Assert.Equal(new AutomationKeyAction(AutomationKeyKind.Select, 0), AutomationKeys.Decide(_left, 3, 0));
        Assert.Equal(new AutomationKeyAction(AutomationKeyKind.Select), AutomationKeys.Decide(_right, 0, null));
    }

    /// <summary>Enter adds after the selected point, and a repeat of Enter adds nothing: holding the key must not stack points.</summary>
    [Fact]
    public void EnterAddsAfterTheSelectedPointButNotOnARepeatOrWithNothingSelected()
    {
        Assert.Equal(new AutomationKeyAction(AutomationKeyKind.Add, 1), AutomationKeys.Decide(_enter, 3, 1));
        Assert.Equal(new AutomationKeyAction(AutomationKeyKind.None), AutomationKeys.Decide(_enter with { Repeat = true }, 3, 1));
        Assert.Equal(new AutomationKeyAction(AutomationKeyKind.None), AutomationKeys.Decide(_enter, 3, null));
    }

    /// <summary>Delete removes the selected point only.</summary>
    [Fact]
    public void DeleteRemovesTheSelectedPoint()
    {
        Assert.Equal(new AutomationKeyAction(AutomationKeyKind.Delete, 1), AutomationKeys.Decide(_delete, 3, 1));
        Assert.Equal(new AutomationKeyAction(AutomationKeyKind.None), AutomationKeys.Decide(_delete, 3, null));
    }

    /// <summary>Alt with an arrow nudges the selected point in that direction, and nudges nothing with no selection.</summary>
    [Fact]
    public void AltArrowsNudgeTheSelectedPoint()
    {
        KeyChord altRight = new("ArrowRight", false, false, true);
        KeyChord altUp = new("ArrowUp", false, false, true);

        Assert.Equal(new AutomationKeyAction(AutomationKeyKind.Nudge, 1, NudgeDirection.Later), AutomationKeys.Decide(altRight, 3, 1));
        Assert.Equal(new AutomationKeyAction(AutomationKeyKind.Nudge, 1, NudgeDirection.Higher), AutomationKeys.Decide(altUp, 3, 1));
        Assert.Equal(new AutomationKeyAction(AutomationKeyKind.None), AutomationKeys.Decide(altRight, 3, null));
    }

    /// <summary>A chord with no binding in a lane does nothing; Shift with an arrow is not a lane key.</summary>
    [Fact]
    public void AnUnboundChordDoesNothing()
    {
        KeyChord shiftLeft = new("ArrowLeft", false, true, false);

        Assert.Equal(new AutomationKeyAction(AutomationKeyKind.None), AutomationKeys.Decide(shiftLeft, 3, 1));
    }

    /// <summary>The gate draws the first time, then only when what it draws changed; a list the document replaced draws even if equal.</summary>
    [Fact]
    public void TheGateDrawsOnlyWhenWhatItDrawsChanged()
    {
        List<AutomationPoint> points = [new AutomationPoint(1, 0.5)];
        AutomationLaneShown shown = Shown(points);
        AutomationLaneGate gate = new();

        Assert.True(gate.Draw(shown));
        Assert.False(gate.Draw(shown));
        Assert.True(gate.Draw(shown with { Selected = 0 }));
        Assert.True(gate.Draw(shown with { Selected = 0, Name = "Renamed" }));
        Assert.True(gate.Draw(shown with { Selected = 0, Name = "Renamed", Dragging = true, DragTime = 2 }));
        Assert.True(gate.Draw(Shown(new List<AutomationPoint>([new AutomationPoint(1, 0.5)]))));
    }

    /// <summary>A first render that was remembered draws nothing again until something changes.</summary>
    [Fact]
    public void ARememberedFirstRenderDoesNotDrawAgain()
    {
        AutomationLaneShown shown = Shown([new AutomationPoint(1, 0.5)]);
        AutomationLaneGate gate = new();

        gate.Remember(shown);

        Assert.False(gate.Draw(shown));
    }

    /// <summary>The words of an edit name the point, its value and its time, so a screen reader says what changed.</summary>
    [Fact]
    public void TheAnnouncementsSayWhatChanged()
    {
        string selected = AutomationAnnouncement.Selected(AutomationParameter.Volume, 1, 3, 1.25, 0.5);
        Assert.Equal("Point 2 of 3 selected: volume 0.50 at 1.25 seconds.", selected);
        Assert.Equal("Point added: pan -0.30 at 4.00 seconds.", AutomationAnnouncement.Added(AutomationParameter.Pan, 4, -0.3));
        Assert.Equal("Point removed; 2 left in the lane.", AutomationAnnouncement.Removed(2));
    }

    private static AutomationLaneShown Shown(IReadOnlyList<AutomationPoint> points) => new(
        points,
        null,
        false,
        null,
        0,
        0,
        100,
        40,
        0,
        "var(--track-1)",
        true,
        null,
        "Track 1",
        AutomationParameter.Volume);
}
