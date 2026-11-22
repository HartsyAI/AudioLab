using AudioLab.Core.Commands;
using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Core.Tests;

public sealed class AutomationCommandTests
{
    private static readonly ClientId _client = ClientId.From("test-client");

    private readonly TrackId _track = TrackId.From("trk_1");

    private readonly CommandRig _rig;

    public AutomationCommandTests()
    {
        _rig = new CommandRig(BlankProject.Create() with { Tracks = [BlankProject.Track("trk_1"), BlankProject.Track("trk_2")] });
    }

    private Track Track => _rig.Project.Tracks[0];

    private ValueList<AutomationPoint> Volume => Track.Automation.Volume;

    private ValueList<AutomationPoint> Pan => Track.Automation.Pan;

    private void Add(AutomationParameter parameter, double time, double value)
    {
        _rig.Ok(new AddAutomationPoint { Project = _rig.Id, Track = _track, Parameter = parameter, Time = time, Value = value });
    }

    private void Seed(params (double Time, double Value)[] points)
    {
        foreach ((double time, double value) in points)
        {
            Add(AutomationParameter.Volume, time, value);
        }
    }

    // ----- add

    [Fact]
    public void APointGoesIntoItsLaneInTimeOrderAndAnotherAtTheSameTimeGoesBehindIt()
    {
        Seed((2, 0.5), (1, 0.4), (2, 0.9), (3, 0.1));
        Add(AutomationParameter.Pan, 1, -0.5);

        Assert.Equal([1.0, 2.0, 2.0, 3.0], Volume.Select(point => point.Time));
        Assert.Equal([0.4, 0.5, 0.9, 0.1], Volume.Select(point => point.Value));
        Assert.Equal([new AutomationPoint(1, -0.5)], Pan);
        Assert.Empty(_rig.Project.Tracks[1].Automation.Volume);
    }

    [Fact]
    public void ThePanLaneTakesMinusOneToOneAndTheVolumeLaneZeroToTheFadersMaximum()
    {
        Add(AutomationParameter.Pan, 0, -1);
        Add(AutomationParameter.Pan, 1, 1);
        Add(AutomationParameter.Volume, 0, 0);
        Add(AutomationParameter.Volume, 1, ProjectLimits.MaxGain);

        (AutomationParameter, double)[] outside =
        [
            (AutomationParameter.Pan, 1.01), (AutomationParameter.Pan, -1.01), (AutomationParameter.Volume, ProjectLimits.MaxGain + 0.01),
            (AutomationParameter.Volume, -0.01), (AutomationParameter.Volume, double.NaN), (AutomationParameter.Pan, double.PositiveInfinity),
        ];
        foreach ((AutomationParameter parameter, double value) in outside)
        {
            _rig.RefusedWith(new AddAutomationPoint { Project = _rig.Id, Track = _track, Parameter = parameter, Time = 1, Value = value }, CommandErrorCode.Invalid);
        }
    }

    [Fact]
    public void ATimeOutsideTheProjectAndATrackThatIsNotThereAreRefusedWithTheRightClass()
    {
        AddAutomationPoint good = new() { Project = _rig.Id, Track = _track, Parameter = AutomationParameter.Volume, Time = 1, Value = 1 };

        _rig.RefusedWith(good with { Time = -0.1 }, CommandErrorCode.Invalid);
        _rig.RefusedWith(good with { Time = ProjectLimits.MaxSeconds + 1 }, CommandErrorCode.Invalid);
        _rig.RefusedWith(good with { Time = double.NaN }, CommandErrorCode.Invalid);
        _rig.RefusedWith(good with { Track = TrackId.From("trk_none"), Time = double.NaN }, CommandErrorCode.NotFound);
        _rig.Ok(good with { Time = 0 });
        _rig.Ok(good with { Time = ProjectLimits.MaxSeconds });
    }

    [Fact]
    public void ALaneHoldsAtMostTheListLimitAndTheRefusalSaysSo()
    {
        ValueList<AutomationPoint> full = [.. Enumerable.Range(0, ProjectLimits.MaxListItems).Select(i => new AutomationPoint(i * 0.5, 0.5))];
        CommandRig rig = new(BlankProject.Create() with { Tracks = [BlankProject.Track("trk_1") with { Automation = BlankProject.Track("x").Automation with { Volume = full } }] });

        CommandResult refused = rig.Refused(new AddAutomationPoint { Project = rig.Id, Track = _track, Parameter = AutomationParameter.Volume, Time = 1, Value = 1 });

        Assert.Equal(CommandErrorCode.Invalid, refused.Errors[0].Code);
        Assert.Contains("as many points as a lane may", refused.Errors[0].Message);
        rig.Ok(new AddAutomationPoint { Project = rig.Id, Track = _track, Parameter = AutomationParameter.Pan, Time = 1, Value = 0 });
    }

    // ----- move and delete

    [Fact]
    public void APointIsMovedToItsTargetAndKeptInTimeOrder()
    {
        Seed((1, 0.1), (2, 0.2), (3, 0.3));

        _rig.Ok(new MoveAutomationPoint { Project = _rig.Id, Track = _track, Parameter = AutomationParameter.Volume, Index = 0, Time = 2.5, Value = 0.7 });

        Assert.Equal([new AutomationPoint(2, 0.2), new AutomationPoint(2.5, 0.7), new AutomationPoint(3, 0.3)], Volume);
    }

    [Fact]
    public void ADragPastANeighbourAndBackIsOneGestureEveryFrameSucceedsAndItEndsExactlyOnItsTarget()
    {
        Seed((1, 0.1), (2, 0.2), (3, 0.3));
        ValueList<AutomationPoint> before = Volume;
        GestureId drag = GestureId.From("point-drag");
        int steps = _rig.History.UndoCount;
        int index = 0;

        // The client follows the point with the rule's own index, which is how it knows which point it is holding.
        foreach ((double time, double value) in new[] { (1.5, 0.3), (2.5, 0.9), (3.5, 1.1), (2.2, 0.4), (1.2, 0.15), (1.0, 0.1) })
        {
            AutomationPoint target = new(time, value);
            LaneEdit expected = AutomationRules.Move(Volume, index, target);
            CommandResult result = _rig.Run(
                new MoveAutomationPoint { Project = _rig.Id, Track = _track, Parameter = AutomationParameter.Volume, Index = index, Time = time, Value = value },
                drag);
            Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(error => error.Message)));
            Assert.Equal(expected.Points, Volume);
            Assert.Equal(target, Volume[expected.Index]);
            index = expected.Index;
        }
        _rig.History.EndGesture(_client, drag);

        Assert.Equal(before, Volume);
        Assert.Equal(steps, _rig.History.UndoCount);
    }

    [Fact]
    public void ADragThatEndsSomewhereElseIsOneUndoStepBackToTheStart()
    {
        Seed((1, 0.1), (2, 0.2), (3, 0.3));
        ValueList<AutomationPoint> before = Volume;
        GestureId drag = GestureId.From("point-drag-2");
        int steps = _rig.History.UndoCount;

        _rig.Run(new MoveAutomationPoint { Project = _rig.Id, Track = _track, Parameter = AutomationParameter.Volume, Index = 0, Time = 1.5, Value = 0.5 }, drag);
        _rig.Run(new MoveAutomationPoint { Project = _rig.Id, Track = _track, Parameter = AutomationParameter.Volume, Index = 0, Time = 2.5, Value = 0.6 }, drag);
        _rig.History.EndGesture(_client, drag);

        Assert.Equal(steps + 1, _rig.History.UndoCount);
        _rig.History.Undo();
        Assert.Equal(before, Volume);
    }

    [Fact]
    public void ADraggedValueInsideTheSchemaButOutsideTheLaneIsKeptInTheLaneAndOneOutsideTheSchemaIsRefused()
    {
        Seed((1, 0.5));
        Add(AutomationParameter.Pan, 1, 0);
        MoveAutomationPoint move = new() { Project = _rig.Id, Track = _track, Parameter = AutomationParameter.Pan, Index = 0, Time = 1, Value = 1.8 };

        _rig.Ok(move);
        Assert.Equal(1.0, Pan[0].Value);
        _rig.Ok(move with { Value = -0.5 });
        _rig.Ok(move with { Parameter = AutomationParameter.Volume, Value = -0.7 });
        Assert.Equal(0.0, Volume[0].Value);
        _rig.Ok(move with { Value = 2.1 });
        Assert.Equal(1.0, Pan[0].Value);
        _rig.RefusedWith(move with { Value = ProjectLimits.MaxGain + 0.1 }, CommandErrorCode.Invalid);
        _rig.RefusedWith(move with { Value = -1.1 }, CommandErrorCode.Invalid);
        _rig.RefusedWith(move with { Value = double.NaN }, CommandErrorCode.Invalid);
        _rig.RefusedWith(move with { Time = -1 }, CommandErrorCode.Invalid);
    }

    [Fact]
    public void APointThatIsNotInTheLaneIsNotFoundBeforeAnyValueIsLookedAt()
    {
        Seed((1, 0.5));
        MoveAutomationPoint move = new() { Project = _rig.Id, Track = _track, Parameter = AutomationParameter.Volume, Index = 1, Time = double.NaN, Value = 0.5 };

        _rig.RefusedWith(move, CommandErrorCode.NotFound);
        _rig.RefusedWith(move with { Index = -1 }, CommandErrorCode.NotFound);
        _rig.RefusedWith(move with { Parameter = AutomationParameter.Pan, Index = 0 }, CommandErrorCode.NotFound);
        _rig.RefusedWith(move with { Track = TrackId.From("trk_none") }, CommandErrorCode.NotFound);
        _rig.RefusedWith(new DeleteAutomationPoint { Project = _rig.Id, Track = _track, Parameter = AutomationParameter.Volume, Index = 5 }, CommandErrorCode.NotFound);
    }

    [Fact]
    public void ADeletedPointLeavesTheOthersInOrder()
    {
        Seed((1, 0.1), (2, 0.2), (3, 0.3));

        _rig.Ok(new DeleteAutomationPoint { Project = _rig.Id, Track = _track, Parameter = AutomationParameter.Volume, Index = 1 });

        Assert.Equal([new AutomationPoint(1, 0.1), new AutomationPoint(3, 0.3)], Volume);
    }

    // ----- segment drag

    [Fact]
    public void ADraggedSegmentSetsItsTwoEndPointsToAbsoluteValuesAndKeepsTheirTimes()
    {
        Seed((1, 0.2), (2, 0.4), (3, 0.6));
        (int left, int right) = AutomationRules.Segment(Volume, 1.5);
        GestureId drag = GestureId.From("segment-drag");
        int steps = _rig.History.UndoCount;

        foreach (double shift in new[] { 0.1, 0.4, 0.9, 1.5, -1.2, 0.2 })
        {
            // The client adds the pointer's shift to each end's start value; the rule keeps the result inside the lane.
            CommandResult result = _rig.Run(
                new SetAutomationValues
                {
                    Project = _rig.Id,
                    Track = _track,
                    Parameter = AutomationParameter.Volume,
                    Points = [new PointValue(left, 0.2 + shift), new PointValue(right, 0.4 + shift)],
                },
                drag);
            Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(error => error.Message)));
        }
        _rig.History.EndGesture(_client, drag);

        Assert.Equal([1.0, 2.0, 3.0], Volume.Select(point => point.Time));
        Assert.Equal([0.4, 0.6, 0.6], Volume.Select(point => Math.Round(point.Value, 9)));
        Assert.Equal(steps + 1, _rig.History.UndoCount);
    }

    [Fact]
    public void ValuesAreKeptInTheLaneTheLastOfTwoForOnePointWinsAndBadListsAreRefused()
    {
        Seed((1, 0.2), (2, 0.4));
        SetAutomationValues set = new() { Project = _rig.Id, Track = _track, Parameter = AutomationParameter.Volume, Points = [new PointValue(0, ProjectLimits.MaxGain + 0.5)] };

        _rig.RefusedWith(set, CommandErrorCode.Invalid);
        _rig.Ok(set with { Points = [new PointValue(0, 1.9), new PointValue(0, 1.5)] });
        Assert.Equal(1.5, Volume[0].Value);
        _rig.Ok(set with { Points = [new PointValue(1, -0.4)] });
        Assert.Equal(0.0, Volume[1].Value);
        _rig.RefusedWith(set with { Points = [] }, CommandErrorCode.Invalid);
        _rig.RefusedWith(set with { Points = [new PointValue(2, 1)] }, CommandErrorCode.NotFound);
        _rig.RefusedWith(set with { Points = [new PointValue(0, 1), new PointValue(-1, double.NaN)] }, CommandErrorCode.NotFound);
        _rig.RefusedWith(set with { Track = TrackId.From("trk_none") }, CommandErrorCode.NotFound);
    }

    // ----- a recorded pass

    [Fact]
    public void ARecordedPassIsFoldedIntoTheLaneAndTheOldCurveTakesOverAgainWhenAsked()
    {
        Seed((0, 1), (10, 0));

        _rig.Ok(new CommitAutomationWrite
        {
            Project = _rig.Id,
            Track = _track,
            Parameter = AutomationParameter.Volume,
            Points = [new WrittenPoint(5, 0.6), new WrittenPoint(4, 0.5)],
            ReturnToOld = true,
        });

        Assert.Equal([0.0, 3.99, 4.0, 5.0, 5.05, 10.0], Volume.Select(point => Math.Round(point.Time, 6)));
    }

    [Fact]
    public void APassThatWroteWhatWasAlreadyThereChangesNothingAndAddsNoUndoStep()
    {
        Seed((0, 0.5), (10, 0.5));
        int steps = _rig.History.UndoCount;
        ProjectDocument before = _rig.Project;

        CommandResult result = _rig.Run(new CommitAutomationWrite
        {
            Project = _rig.Id,
            Track = _track,
            Parameter = AutomationParameter.Volume,
            Points = [new WrittenPoint(2, 0.5), new WrittenPoint(3, 0.5)],
            ReturnToOld = true,
        });

        Assert.True(result.Succeeded);
        Assert.Equal(before, _rig.Project);
        Assert.Equal(steps, _rig.History.UndoCount);
    }

    [Fact]
    public void AWritePassThatEndsAsTouchIsOneUndoStepAndEveryCommandOfItSucceeds()
    {
        Seed((0, 1));
        _rig.Ok(new SetAutomationMode { Project = _rig.Id, Track = _track, Mode = AutomationMode.Write });
        ProjectDocument before = _rig.Project;
        GestureId pass = GestureId.From("write-pass");
        int steps = _rig.History.UndoCount;

        CommandResult volume = _rig.Run(
            new CommitAutomationWrite
            {
                Project = _rig.Id,
                Track = _track,
                Parameter = AutomationParameter.Volume,
                ReturnToOld = true,
                Points = [new WrittenPoint(1, 0.4), new WrittenPoint(2, 0.8)],
            },
            pass);
        CommandResult pan = _rig.Run(
            new CommitAutomationWrite
            {
                Project = _rig.Id,
                Track = _track,
                Parameter = AutomationParameter.Pan,
                ReturnToOld = true,
                Points = [new WrittenPoint(1, -0.5), new WrittenPoint(2, 0.5)],
            },
            pass);
        CommandResult mode = _rig.Run(new SetAutomationMode { Project = _rig.Id, Track = _track, Mode = AutomationMode.Touch }, pass);
        _rig.History.EndGesture(_client, pass);

        Assert.True(volume.Succeeded && pan.Succeeded && mode.Succeeded);
        Assert.Equal(AutomationMode.Touch, Track.Automation.Mode);
        Assert.NotEmpty(Pan);
        Assert.Equal(steps + 1, _rig.History.UndoCount);
        _rig.History.Undo();
        Assert.Equal(before, _rig.Project);
    }

    [Fact]
    public void APassWithAValueOutsideItsLaneOrATimeOutsideTheProjectOrNoPointsIsRefused()
    {
        CommitAutomationWrite commit = new()
        {
            Project = _rig.Id,
            Track = _track,
            Parameter = AutomationParameter.Pan,
            Points = [new WrittenPoint(1, 0.5)],
            ReturnToOld = false,
        };

        _rig.RefusedWith(commit with { Points = [new WrittenPoint(1, 1.5)] }, CommandErrorCode.Invalid);
        _rig.RefusedWith(commit with { Points = [new WrittenPoint(-1, 0.5)] }, CommandErrorCode.Invalid);
        _rig.RefusedWith(commit with { Points = [new WrittenPoint(1, double.NaN)] }, CommandErrorCode.Invalid);
        _rig.RefusedWith(commit with { Points = [] }, CommandErrorCode.Invalid);
        _rig.RefusedWith(commit with { Track = TrackId.From("trk_none"), Points = [] }, CommandErrorCode.NotFound);
        _rig.Ok(commit with { Parameter = AutomationParameter.Volume, Points = [new WrittenPoint(1, 1.5)] });
    }

    [Fact]
    public void EveryPassOfRandomPointsLeavesAValidLaneInTimeOrder()
    {
        Random random = new(7);
        Seed((0, 1), (20, 0.1));
        foreach (int pass in Enumerable.Range(0, 60))
        {
            List<WrittenPoint> points = [.. Enumerable.Range(0, random.Next(1, 12)).Select(_ => new WrittenPoint(Math.Round(random.NextDouble() * 25, 3), Math.Round(random.NextDouble() * 2, 3)))];
            CommandResult result = _rig.Run(new CommitAutomationWrite
            {
                Project = _rig.Id,
                Track = _track,
                Parameter = AutomationParameter.Volume,
                ReturnToOld = pass % 2 == 0,
                Points = [.. points],
            });

            Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(error => error.Message)));
            Assert.True(Volume.Select(point => point.Time).SequenceEqual(Volume.Select(point => point.Time).Order()));
        }
    }

    // ----- mode and view

    [Fact]
    public void TheModeIsSetAndSettingItAgainChangesNothing()
    {
        int steps = _rig.History.UndoCount;

        _rig.Ok(new SetAutomationMode { Project = _rig.Id, Track = _track, Mode = AutomationMode.Latch });
        Assert.Equal(AutomationMode.Latch, Track.Automation.Mode);
        _rig.Ok(new SetAutomationMode { Project = _rig.Id, Track = _track, Mode = AutomationMode.Latch });

        Assert.Equal(steps + 1, _rig.History.UndoCount);
        _rig.RefusedWith(new SetAutomationMode { Project = _rig.Id, Track = TrackId.From("trk_none"), Mode = AutomationMode.Off }, CommandErrorCode.NotFound);
        _rig.RefusedWith(new SetAutomationMode { Project = _rig.Id, Track = _track, Mode = (AutomationMode)99 }, CommandErrorCode.Invalid);
    }

    [Fact]
    public void TheLaneViewTakesEitherPartAndKeepsTheOtherAndNeitherIsRefused()
    {
        _rig.Ok(new SetAutomationView { Project = _rig.Id, Track = _track, Visible = true });
        Assert.Equal((true, AutomationParameter.Volume), (Track.View.AutomationVisible, Track.View.AutomationParameter));
        _rig.Ok(new SetAutomationView { Project = _rig.Id, Track = _track, Parameter = AutomationParameter.Pan });
        Assert.Equal((true, AutomationParameter.Pan), (Track.View.AutomationVisible, Track.View.AutomationParameter));
        _rig.Ok(new SetAutomationView { Project = _rig.Id, Track = _track, Visible = false, Parameter = AutomationParameter.Volume });
        Assert.Equal((false, AutomationParameter.Volume), (Track.View.AutomationVisible, Track.View.AutomationParameter));

        _rig.RefusedWith(new SetAutomationView { Project = _rig.Id, Track = _track }, CommandErrorCode.Invalid);
        _rig.RefusedWith(new SetAutomationView { Project = _rig.Id, Track = TrackId.From("trk_none") }, CommandErrorCode.NotFound);
    }
}
