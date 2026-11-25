using AudioLab.Core.Model;
using AudioLab.Core.Rules;
using AudioLab.Web.Screens;
using AudioLab.Web.Session;

namespace AudioLab.Web.Tests;

/// <summary>Holds the automation view state tests: what a change raises, the nudges, and the rows of the layout.</summary>
public sealed class AutomationLanesTests
{
    private static readonly TrackId _track = TrackId.From("t1");

    private static readonly AutomationLanePlan _volume = new(AutomationParameter.Volume, TimelineGeometry.Default, 40);

    /// <summary>A change names the one track whose lane it touches, and a selection that changes nothing raises nothing.</summary>
    [Fact]
    public void AChangeNamesOnlyTheLaneItTouches()
    {
        AutomationLanes lanes = new();
        List<TrackId> raised = [];
        lanes.Changed += raised.Add;

        lanes.Select(_track, 1);
        lanes.Select(_track, 1);
        lanes.Select(TrackId.From("t2"), 0);

        Assert.Equal([_track, _track, TrackId.From("t2")], raised);
    }

    /// <summary>A prune raises a change only for what it drops, so a selection or drag the document keeps costs no redraw.</summary>
    [Fact]
    public void PruneRaisesOnlyWhenASelectionOrDragWasDropped()
    {
        ProjectDocument document = Document(3);
        AutomationLanes lanes = new();
        List<TrackId> raised = [];
        lanes.Changed += raised.Add;
        lanes.Select(_track, 1);
        raised.Clear();

        bool keptDrag = lanes.Prune(document);
        Assert.False(keptDrag);
        Assert.Empty(raised);

        ProjectDocument shorter = Document(1);
        lanes.Prune(shorter);
        Assert.Null(lanes.Track);
        Assert.Equal([_track], raised);

        raised.Clear();
        lanes.Prune(shorter);
        Assert.Empty(raised);
    }

    /// <summary>A drag whose lane another client changed is dropped, and the pointer must be let go.</summary>
    [Fact]
    public void ADragOfALaneThatChangedIsDropped()
    {
        ProjectDocument document = Document(3);
        Track track = document.Tracks[0];
        AutomationDrag drag = new(_track, AutomationRules.Lane(track.Automation, AutomationParameter.Volume), 1, _volume, 0, 0);
        AutomationLanes lanes = new();
        lanes.Begin(drag);

        Assert.False(lanes.Prune(document));
        Assert.True(lanes.Prune(Document(3)));
        Assert.Null(lanes.Drag);
    }

    /// <summary>Two quick nudges of the same point move it twice: the second is built from the lane after the first reply.</summary>
    [Fact]
    public void TwoQuickNudgesMoveTheSamePointTwice()
    {
        IReadOnlyList<AutomationPoint> lane = [new AutomationPoint(1, 0.5), new AutomationPoint(2, 0.2)];

        AutomationMoveTarget? first = AutomationNudge.Target(lane, 0, AutomationParameter.Volume, NudgeDirection.Later, 1);
        Assert.NotNull(first);
        IReadOnlyList<AutomationPoint> after = AutomationRules.Move(lane, 0, new AutomationPoint(first.Value.Time, first.Value.Value)).Points;
        Assert.Equal(0.5, after[first.Value.LandsAt].Value, 6);

        AutomationMoveTarget? second = AutomationNudge.Target(after, first.Value.LandsAt, AutomationParameter.Volume, NudgeDirection.Later, 1);

        Assert.NotNull(second);
        Assert.Equal(3, second.Value.Time, 6);
        Assert.Equal(0.5, second.Value.Value, 6);
    }

    /// <summary>A value step is a tenth of the drawn range, so one step is visible at a lane's height.</summary>
    [Fact]
    public void AValueStepIsVisibleAtTheLaneHeight()
    {
        IReadOnlyList<AutomationPoint> lane = [new AutomationPoint(1, 0.5)];

        AutomationMoveTarget? up = AutomationNudge.Target(lane, 0, AutomationParameter.Volume, NudgeDirection.Higher, 1);

        Assert.NotNull(up);
        double pixels = _volume.Y(0.5) - _volume.Y(up.Value.Value);
        Assert.True(pixels >= 3.9, $"{pixels} px");
    }

    /// <summary>The layout gives an open automation lane its own row below the clip lane, and a closed one none.</summary>
    [Fact]
    public void TheLayoutGivesAnOpenLaneItsOwnRow()
    {
        ProjectDocument document = Document(3, visibleFirst: false, secondVisible: true);

        TrackLayout layout = TrackLayout.Of(document);

        Assert.Equal(64, layout.ClipHeights[0]);
        Assert.Equal(64, layout.Heights[0]);
        Assert.Equal(64 + TrackLayout.AutomationHeight, layout.Heights[1]);
        Assert.Equal(64, layout.Tops[1]);
        Assert.Equal(64 + 64, layout.AutomationTop(1));
        Assert.Equal(1, layout.RowAt(64 + 64 + 5));
        Assert.Equal(0, layout.RowAt(10));
    }

    private static ProjectDocument Document(int points, bool visibleFirst = true, bool secondVisible = false)
    {
        List<AutomationPoint> lane = [];
        for (int i = 0; i < points; i++)
        {
            lane.Add(new AutomationPoint(i + 1, 0.5));
        }
        Track track = Song.NewTrack("t1", 64);
        track = track with
        {
            Automation = AutomationRules.WithLane(track.Automation, AutomationParameter.Volume, [.. lane]),
            View = track.View with { AutomationVisible = visibleFirst, AutomationParameter = AutomationParameter.Volume },
        };
        TrackView view = new() { Height = 64, AutomationVisible = secondVisible, AutomationParameter = AutomationParameter.Volume };
        Track second = Song.NewTrack("t2", 64) with { View = view };
        return Song.Empty().With(track, second);
    }
}
