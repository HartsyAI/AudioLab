using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Core.Tests;

public sealed class AutomationRuleTests
{
    private static ValueList<AutomationPoint> Lane(params (double Time, double Value)[] points)
    {
        return [.. points.Select(point => new AutomationPoint(point.Time, point.Value))];
    }

    private static ProjectDocument WithLane(AutomationParameter parameter, ValueList<AutomationPoint> points)
    {
        Track track = BlankProject.Track("trk_1");
        return BlankProject.Create() with { Tracks = [track with { Automation = AutomationRules.WithLane(track.Automation, parameter, points) }] };
    }

    [Fact]
    public void ALaneWithNoPointsLeavesTheControlAloneAndOneWithPointsHoldsItsEnds()
    {
        ValueList<AutomationPoint> lane = Lane((1, 0.2), (3, 1.0));

        Assert.Null(AutomationRules.ValueAt([], 2));
        Assert.Equal(0.2, AutomationRules.ValueAt(lane, 0));
        Assert.Equal(0.2, AutomationRules.ValueAt(lane, 1));
        Assert.Equal(0.6, AutomationRules.ValueAt(lane, 2)!.Value, 9);
        Assert.Equal(1.0, AutomationRules.ValueAt(lane, 3));
        Assert.Equal(1.0, AutomationRules.ValueAt(lane, 99));
    }

    [Fact]
    public void AStepMadeOfTwoPointsAtOneTimeReadsTheEarlierValueAtTheStepAndTheLaterOneAfterIt()
    {
        ValueList<AutomationPoint> lane = Lane((0, 0), (1, 0.2), (1, 0.8), (2, 1));

        Assert.Equal(0.2, AutomationRules.ValueAt(lane, 1));
        Assert.Equal(0.8, AutomationRules.ValueAt(lane, 1.0000001)!.Value, 5);
        Assert.Equal(0.9, AutomationRules.ValueAt(lane, 1.5)!.Value, 9);
    }

    [Theory]
    [InlineData(AutomationParameter.Volume, 0, 2, 1)]
    [InlineData(AutomationParameter.Pan, -1, 1, 0)]
    public void EachLaneHasItsRangeAndItsIdentity(AutomationParameter parameter, double min, double max, double identity)
    {
        Assert.Equal((min, max, identity), (AutomationRules.Min(parameter), AutomationRules.Max(parameter), AutomationRules.Identity(parameter)));
        Assert.Equal(max, AutomationRules.Clamp(parameter, 9));
        Assert.Equal(min, AutomationRules.Clamp(parameter, -9));
    }

    [Theory]
    [InlineData(AutomationParameter.Volume)]
    [InlineData(AutomationParameter.Pan)]
    public void TheLaneRangesAreInsideWhatTheValidatorAccepts(AutomationParameter parameter)
    {
        double min = AutomationRules.Min(parameter);
        double max = AutomationRules.Max(parameter);

        Assert.Empty(ProjectValidator.Validate(WithLane(parameter, Lane((0, min), (1, max)))));
        Assert.NotEmpty(ProjectValidator.Validate(WithLane(parameter, Lane((0, min - 0.01)))));
        if (parameter == AutomationParameter.Pan)
        {
            Assert.NotEmpty(ProjectValidator.Validate(WithLane(parameter, Lane((0, max + 0.01)))));
        }
        else
        {
            Assert.True(max < ProjectLimits.MaxGain, "the old lane stopped at 2 while the fader goes to 16");
        }
    }

    [Fact]
    public void APointIsInsertedBehindPointsAtItsTimeAndTheIndexSaysWhereItLanded()
    {
        ValueList<AutomationPoint> lane = Lane((1, 0.1), (2, 0.2), (2, 0.3), (4, 0.4));

        LaneEdit middle = AutomationRules.Insert(lane, new AutomationPoint(2, 0.9));
        LaneEdit first = AutomationRules.Insert(lane, new AutomationPoint(0, 0.9));
        LaneEdit last = AutomationRules.Insert(lane, new AutomationPoint(9, 0.9));

        Assert.Equal(3, middle.Index);
        Assert.Equal(0.9, middle.Points[3].Value);
        Assert.Equal((0, 0), (first.Index, (int)first.Points[0].Time));
        Assert.Equal(4, last.Index);
        Assert.Equal(5, last.Points.Count);
    }

    [Fact]
    public void AMovedPointIsPutBackInTimeOrderAndTheIndexFollowsIt()
    {
        ValueList<AutomationPoint> lane = Lane((1, 0.1), (2, 0.2), (3, 0.3));

        LaneEdit past = AutomationRules.Move(lane, 0, new AutomationPoint(2.5, 0.7));
        LaneEdit back = AutomationRules.Move(past.Points, past.Index, new AutomationPoint(1, 0.1));

        Assert.Equal(1, past.Index);
        Assert.Equal([2.0, 2.5, 3.0], past.Points.Select(point => point.Time));
        Assert.Equal(0, back.Index);
        Assert.Equal(lane, back.Points);
    }

    [Fact]
    public void AGrabOnTheLineMovesThePointsOnEitherSideAndBeforeTheFirstOrAfterTheLastJustThatOne()
    {
        ValueList<AutomationPoint> lane = Lane((1, 0.1), (2, 0.2), (3, 0.3));

        Assert.Equal((0, 1), AutomationRules.Segment(lane, 1.5));
        Assert.Equal((1, 2), AutomationRules.Segment(lane, 2));
        Assert.Equal((-1, 0), AutomationRules.Segment(lane, 0.5));
        Assert.Equal((2, -1), AutomationRules.Segment(lane, 9));
        Assert.Equal((-1, -1), AutomationRules.Segment([], 1));
    }

    [Fact]
    public void ATouchPassLeadsInFromTheOldCurveWritesItsPointsAndHandsBackJustAfter()
    {
        ValueList<AutomationPoint> lane = Lane((0, 1), (10, 0.0));

        ValueList<AutomationPoint> merged = AutomationRules.Commit(lane, [new(4, 0.5), new(5, 0.6)], AutomationParameter.Volume, returnToOld: true);

        Assert.Equal([0.0, 3.99, 4.0, 5.0, 5.05, 10.0], merged.Select(point => point.Time).Select(time => Math.Round(time, 6)));
        Assert.Equal(AutomationRules.ValueAt(lane, 3.99)!.Value, merged[1].Value, 9);
        Assert.Equal(0.5, merged[2].Value);
        Assert.Equal(AutomationRules.ValueAt(lane, 5.05)!.Value, merged[4].Value, 9);
    }

    [Fact]
    public void ALatchPassKeepsItsLastValueAndTheOldPointsAfterItInsteadOfHandingBack()
    {
        ValueList<AutomationPoint> lane = Lane((0, 1), (10, 0.0));

        ValueList<AutomationPoint> merged = AutomationRules.Commit(lane, [new(4, 0.5), new(5, 0.6)], AutomationParameter.Volume, returnToOld: false);

        Assert.Equal([0.0, 3.99, 4.0, 5.0, 10.0], merged.Select(point => Math.Round(point.Time, 6)));
        Assert.Equal((0.6, 0.0), (merged[3].Value, merged[4].Value));
    }

    [Fact]
    public void APassThatWroteWhatWasAlreadyThereChangesNothing()
    {
        ValueList<AutomationPoint> lane = Lane((0, 0.5), (10, 0.5));

        Assert.Equal(lane, AutomationRules.Commit(lane, [new(2, 0.5), new(3, 0.50001)], AutomationParameter.Volume, returnToOld: true));
        Assert.Equal(Lane(), AutomationRules.Commit([], [new(2, 1), new(3, 1)], AutomationParameter.Volume, returnToOld: true));
        Assert.Equal(Lane(), AutomationRules.Commit([], [new(2, 0)], AutomationParameter.Pan, returnToOld: false));
    }

    [Fact]
    public void AHeldValueKeepsOnlyTheEndsOfItsRunAndPointsWithinATenthOfAMillisecondMerge()
    {
        ValueList<AutomationPoint> held = AutomationRules.Commit(
            [],
            [new(1, 0.2), new(1.1, 0.2), new(1.2, 0.2), new(1.3, 0.2), new(1.30005, 0.9)],
            AutomationParameter.Volume,
            returnToOld: false);

        Assert.DoesNotContain(held, point => Math.Abs(point.Time - 1.1) < 1e-9 || Math.Abs(point.Time - 1.2) < 1e-9);
        Assert.Equal(0.2, held.First(point => Math.Abs(point.Time - 1) < 1e-9).Value);
        Assert.Equal(0.2, held[^1].Value);
    }

    [Fact]
    public void APassAtTheStartNeedsNoLeadInAndTheResultIsAlwaysInTimeOrderAndValid()
    {
        ValueList<AutomationPoint> merged = AutomationRules.Commit(Lane((0, 1), (6, 0)), [new(0.005, 0.3), new(0, 0.4)], AutomationParameter.Volume, true);

        Assert.True(merged.Select(point => point.Time).SequenceEqual(merged.Select(point => point.Time).Order()));
        Assert.All(merged, point => Assert.True(point.Time >= 0));
        Assert.Empty(ProjectValidator.Validate(WithLane(AutomationParameter.Volume, merged)));
    }
}
