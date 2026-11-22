using AudioLab.Core.Commands;
using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Core.Tests;

public sealed class BeatCommandTests
{
    private static readonly ClientId _client = ClientId.From("test-client");

    private readonly CommandRig _rig = new();

    private BeatPattern Pattern => _rig.Project.BeatPattern;

    private BeatLaneId Add(string? name = null, string? media = null)
    {
        return BeatLaneId.From(_rig.Created(new AddBeatLane { Project = _rig.Id, Name = name, Media = media is null ? null : MediaId.From(media) })[0]);
    }

    private BeatLane Lane(BeatLaneId id) => Pattern.Lanes.First(lane => lane.Id == id);

    // ----- lanes

    [Fact]
    public void ALaneIsAddedAtTheBottomWithUnityGainAndEveryStepOffAndTheIdIsReported()
    {
        BeatLaneId kick = Add("Kick", "med_kick");
        BeatLaneId second = Add();

        Assert.Equal([kick, second], Pattern.Lanes.Select(lane => lane.Id));
        Assert.Equal(("Kick", "med_kick", 1.0), (Lane(kick).Name, Lane(kick).Media!.Value.Value, Lane(kick).Gain));
        Assert.Equal(16, Lane(kick).Steps.Count);
        Assert.DoesNotContain(true, Lane(kick).Steps);
        Assert.Equal("Pad 2", Lane(second).Name);
        Assert.Null(Lane(second).Media);
    }

    [Fact]
    public void ABlankNameBecomesPadNAndANameOverTheLimitIsRefusedAsSent()
    {
        BeatLaneId blank = Add("   ");

        Assert.Equal("Pad 1", Lane(blank).Name);
        _rig.RefusedWith(new AddBeatLane { Project = _rig.Id, Name = new string('a', ProjectLimits.MaxName + 1) }, CommandErrorCode.Invalid);
        _rig.Ok(new AddBeatLane { Project = _rig.Id, Name = new string('a', ProjectLimits.MaxName) });
    }

    [Fact]
    public void ANewLaneTakesTheStepCountOfAPatternThatWasResized()
    {
        _rig.Ok(new SetBeatSteps { Project = _rig.Id, Steps = 32 });

        BeatLaneId lane = Add("Hat");

        Assert.Equal(32, Lane(lane).Steps.Count);
    }

    [Fact]
    public void ThePatternHoldsAtMostTheLaneLimitAndTheRefusalBurnsNoId()
    {
        BeatLaneId first = Add();
        foreach (int _ in Enumerable.Range(1, ProjectLimits.MaxBeatLanes - 1))
        {
            Add();
        }

        CommandResult refused = _rig.Refused(new AddBeatLane { Project = _rig.Id });

        Assert.Equal(CommandErrorCode.Invalid, refused.Errors[0].Code);
        _rig.Ok(new RemoveBeatLane { Project = _rig.Id, Lane = first });
        BeatLaneId next = Add();
        Assert.Equal($"bln_{1001 + ProjectLimits.MaxBeatLanes}", next.Value);
    }

    [Fact]
    public void ALaneIsRemovedWithItsStepsAndAMissingOneIsNotFound()
    {
        BeatLaneId kick = Add("Kick");
        BeatLaneId snare = Add("Snare");

        _rig.Ok(new RemoveBeatLane { Project = _rig.Id, Lane = kick });

        Assert.Equal([snare], Pattern.Lanes.Select(lane => lane.Id));
        _rig.RefusedWith(new RemoveBeatLane { Project = _rig.Id, Lane = kick }, CommandErrorCode.NotFound);
    }

    [Fact]
    public void ALaneIsRenamedAndABlankOrOverlongNameIsRefusedAfterTheLaneIsFound()
    {
        BeatLaneId kick = Add("Kick");

        _rig.Ok(new RenameBeatLane { Project = _rig.Id, Lane = kick, Name = "Big kick" });

        Assert.Equal("Big kick", Lane(kick).Name);
        _rig.RefusedWith(new RenameBeatLane { Project = _rig.Id, Lane = kick, Name = "  " }, CommandErrorCode.Rejected);
        _rig.RefusedWith(new RenameBeatLane { Project = _rig.Id, Lane = kick, Name = new string('a', 201) }, CommandErrorCode.Invalid);
        _rig.RefusedWith(new RenameBeatLane { Project = _rig.Id, Lane = BeatLaneId.From("bln_none"), Name = " " }, CommandErrorCode.NotFound);
    }

    [Fact]
    public void TheGainIsSetFromZeroToTheMaximumAndAFaderDragIsOneGesture()
    {
        BeatLaneId kick = Add("Kick");
        GestureId drag = GestureId.From("gain-drag");
        int steps = _rig.History.UndoCount;

        foreach (double gain in new[] { 0.9, 0.5, 1.2, 1.5 })
        {
            Assert.True(_rig.Run(new SetBeatLaneGain { Project = _rig.Id, Lane = kick, Gain = gain }, drag).Succeeded);
        }
        _rig.History.EndGesture(_client, drag);

        Assert.Equal(1.5, Lane(kick).Gain);
        Assert.Equal(steps + 1, _rig.History.UndoCount);
        _rig.Ok(new SetBeatLaneGain { Project = _rig.Id, Lane = kick, Gain = 0 });
        _rig.Ok(new SetBeatLaneGain { Project = _rig.Id, Lane = kick, Gain = ProjectLimits.MaxGain });
        _rig.RefusedWith(new SetBeatLaneGain { Project = _rig.Id, Lane = kick, Gain = -0.1 }, CommandErrorCode.Invalid);
        _rig.RefusedWith(new SetBeatLaneGain { Project = _rig.Id, Lane = kick, Gain = ProjectLimits.MaxGain + 0.1 }, CommandErrorCode.Invalid);
        _rig.RefusedWith(new SetBeatLaneGain { Project = _rig.Id, Lane = kick, Gain = double.NaN }, CommandErrorCode.Invalid);
    }

    [Fact]
    public void TheSampleOfALaneIsReplacedOrClearedAndItsStepsStay()
    {
        BeatLaneId kick = Add("Kick", "med_a");
        _rig.Ok(new SetBeatLaneSteps { Project = _rig.Id, Lane = kick, Steps = [0, 4], On = true });

        _rig.Ok(new SetBeatLaneMedia { Project = _rig.Id, Lane = kick, Media = MediaId.From("med_b") });
        Assert.Equal("med_b", Lane(kick).Media!.Value.Value);
        _rig.Ok(new SetBeatLaneMedia { Project = _rig.Id, Lane = kick });

        Assert.Null(Lane(kick).Media);
        Assert.Equal([0, 4], Lane(kick).Steps.Select((on, step) => (on, step)).Where(pair => pair.on).Select(pair => pair.step));
        Assert.DoesNotContain(MediaId.From("med_a"), ProjectContent.ReferencedMedia(_rig.Project));
        _rig.RefusedWith(new SetBeatLaneMedia { Project = _rig.Id, Lane = BeatLaneId.From("bln_none") }, CommandErrorCode.NotFound);
    }

    // ----- steps

    [Fact]
    public void StepsAreSwitchedOnAndOffAndTheOthersStayAsTheyWere()
    {
        BeatLaneId kick = Add("Kick");
        BeatLaneId snare = Add("Snare");

        _rig.Ok(new SetBeatLaneSteps { Project = _rig.Id, Lane = kick, Steps = [0, 4, 8, 12], On = true });
        _rig.Ok(new SetBeatLaneSteps { Project = _rig.Id, Lane = kick, Steps = [4, 12], On = false });

        Assert.Equal(new[] { 0, 8 }, Lane(kick).Steps.Select((on, step) => (on, step)).Where(pair => pair.on).Select(pair => pair.step));
        Assert.DoesNotContain(true, Lane(snare).Steps);
    }

    [Fact]
    public void SettingAStepThatIsAlreadySetChangesNothingAndAddsNoUndoStep()
    {
        BeatLaneId kick = Add("Kick");
        _rig.Ok(new SetBeatLaneSteps { Project = _rig.Id, Lane = kick, Steps = [3], On = true });
        int steps = _rig.History.UndoCount;
        ProjectDocument before = _rig.Project;

        Assert.True(_rig.Run(new SetBeatLaneSteps { Project = _rig.Id, Lane = kick, Steps = [3, 3], On = true }).Succeeded);
        Assert.True(_rig.Run(new SetBeatLaneSteps { Project = _rig.Id, Lane = kick, Steps = [5], On = false }).Succeeded);

        Assert.Equal(before, _rig.Project);
        Assert.Equal(steps, _rig.History.UndoCount);
    }

    [Fact]
    public void AStepPastThePatternIsOutOfRangeEvenWhenTheSchemaWouldAllowItAndTheLaneIsFoundFirst()
    {
        BeatLaneId kick = Add("Kick");
        SetBeatLaneSteps set = new() { Project = _rig.Id, Lane = kick, Steps = [0], On = true };

        _rig.RefusedWith(set with { Steps = [16] }, CommandErrorCode.Invalid);
        _rig.RefusedWith(set with { Steps = [0, 255] }, CommandErrorCode.Invalid);
        _rig.RefusedWith(set with { Steps = [-1] }, CommandErrorCode.Invalid);
        _rig.RefusedWith(set with { Steps = [] }, CommandErrorCode.Invalid);
        _rig.RefusedWith(set with { Lane = BeatLaneId.From("bln_none"), Steps = [999] }, CommandErrorCode.NotFound);
        _rig.Ok(set with { Steps = [15] });
        _rig.Ok(new SetBeatSteps { Project = _rig.Id, Steps = 256 });
        _rig.Ok(set with { Steps = [255] });
    }

    [Fact]
    public void PaintingAcrossStepsIsOneGestureWithEveryFrameSucceeding()
    {
        BeatLaneId kick = Add("Kick");
        GestureId paint = GestureId.From("paint");
        int steps = _rig.History.UndoCount;

        foreach (int step in Enumerable.Range(0, 8))
        {
            CommandResult result = _rig.Run(new SetBeatLaneSteps { Project = _rig.Id, Lane = kick, Steps = [step], On = true }, paint);
            Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(error => error.Message)));
        }
        _rig.History.EndGesture(_client, paint);

        Assert.Equal(8, Lane(kick).Steps.Count(on => on));
        Assert.Equal(steps + 1, _rig.History.UndoCount);
        _rig.History.Undo();
        Assert.DoesNotContain(true, Lane(kick).Steps);
    }

    [Fact]
    public void ResizingThePatternKeepsLeadingStepsPadsWithOffAndKeepsEveryLaneTheSameLength()
    {
        BeatLaneId kick = Add("Kick");
        BeatLaneId snare = Add("Snare");
        _rig.Ok(new SetBeatLaneSteps { Project = _rig.Id, Lane = kick, Steps = [0, 8, 15], On = true });

        _rig.Ok(new SetBeatSteps { Project = _rig.Id, Steps = 32 });
        Assert.Equal((32, 32, 32), (Pattern.Steps, Lane(kick).Steps.Count, Lane(snare).Steps.Count));
        Assert.Equal(3, Lane(kick).Steps.Count(on => on));
        _rig.Ok(new SetBeatSteps { Project = _rig.Id, Steps = 8 });

        Assert.Equal((8, 8, 8), (Pattern.Steps, Lane(kick).Steps.Count, Lane(snare).Steps.Count));
        Assert.Equal(new[] { 0 }, Lane(kick).Steps.Select((on, step) => (on, step)).Where(pair => pair.on).Select(pair => pair.step));
        _rig.History.Undo();
        Assert.Equal(3, Lane(kick).Steps.Count(on => on));
    }

    [Fact]
    public void TheStepCountIsOneToTwoHundredFiftySixAndTheSameCountChangesNothing()
    {
        int steps = _rig.History.UndoCount;

        Assert.True(_rig.Run(new SetBeatSteps { Project = _rig.Id, Steps = 16 }).Succeeded);
        Assert.Equal(steps, _rig.History.UndoCount);
        _rig.Ok(new SetBeatSteps { Project = _rig.Id, Steps = 1 });
        _rig.Ok(new SetBeatSteps { Project = _rig.Id, Steps = ProjectLimits.MaxBeatSteps });
        _rig.RefusedWith(new SetBeatSteps { Project = _rig.Id, Steps = 0 }, CommandErrorCode.Invalid);
        _rig.RefusedWith(new SetBeatSteps { Project = _rig.Id, Steps = ProjectLimits.MaxBeatSteps + 1 }, CommandErrorCode.Invalid);
        _rig.RefusedWith(new SetBeatSteps { Project = _rig.Id, Steps = -16 }, CommandErrorCode.Invalid);
    }

    // ----- swing

    [Fact]
    public void TheSwingIsZeroToOneAndASliderDragIsOneGesture()
    {
        GestureId drag = GestureId.From("swing-drag");
        int steps = _rig.History.UndoCount;

        foreach (double swing in new[] { 0.1, 0.35, 0.6 })
        {
            Assert.True(_rig.Run(new SetBeatSwing { Project = _rig.Id, Swing = swing }, drag).Succeeded);
        }
        _rig.History.EndGesture(_client, drag);

        Assert.Equal(0.6, Pattern.Swing);
        Assert.Equal(steps + 1, _rig.History.UndoCount);
        _rig.Ok(new SetBeatSwing { Project = _rig.Id, Swing = 1 });
        _rig.Ok(new SetBeatSwing { Project = _rig.Id, Swing = 0 });
        _rig.RefusedWith(new SetBeatSwing { Project = _rig.Id, Swing = 1.01 }, CommandErrorCode.Invalid);
        _rig.RefusedWith(new SetBeatSwing { Project = _rig.Id, Swing = -0.01 }, CommandErrorCode.Invalid);
        _rig.RefusedWith(new SetBeatSwing { Project = _rig.Id, Swing = double.NaN }, CommandErrorCode.Invalid);
    }
}
