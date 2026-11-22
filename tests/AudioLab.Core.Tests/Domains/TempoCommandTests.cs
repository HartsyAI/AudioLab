using AudioLab.Core.Commands;
using AudioLab.Core.Model;

namespace AudioLab.Core.Tests;

public sealed class TempoCommandTests
{
    private readonly CommandRig _rig = new();

    [Fact]
    public void TheProjectTempoIsSetAndUndone()
    {
        _rig.Ok(new SetTempoBpm { Project = _rig.Id, Bpm = 96.5 });

        Assert.Equal(96.5, _rig.Project.Bpm);
        _rig.History.Undo();
        Assert.Equal(120, _rig.Project.Bpm);
    }

    [Theory]
    [InlineData(0.5)]
    [InlineData(5000)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void ATempoOutsideTheProjectLimitsIsRefused(double bpm)
    {
        CommandResult result = _rig.Refused(new SetTempoBpm { Project = _rig.Id, Bpm = bpm });

        Assert.Single(result.Errors);
    }

    [Fact]
    public void TheTimeSignatureIsSetAndUndoneWithTheRestOfTheDocument()
    {
        _rig.Ok(new SetTimeSignature { Project = _rig.Id, Numerator = 7, Denominator = 8 });

        Assert.Equal(new TimeSignature(7, 8), _rig.Project.TimeSignature);
        _rig.History.Undo();
        Assert.Equal(new TimeSignature(4, 4), _rig.Project.TimeSignature);
    }

    [Theory]
    [InlineData(0, 4)]
    [InlineData(65, 4)]
    [InlineData(4, 3)]
    [InlineData(4, 64)]
    public void ASignatureThatIsNotOneIsRefused(int numerator, int denominator)
    {
        _rig.RefusedWith(
            new SetTimeSignature { Project = _rig.Id, Numerator = numerator, Denominator = denominator },
            CommandErrorCode.Invalid);
    }

    [Fact]
    public void ATempoChangeIsAddedInTimeOrderWithAServerAllocatedId()
    {
        IReadOnlyList<string> late = _rig.Created(new AddTempoChange { Project = _rig.Id, Time = 8, Bpm = 90 });
        IReadOnlyList<string> early = _rig.Created(new AddTempoChange { Project = _rig.Id, Time = 4, Bpm = 60 });

        Assert.Equal(["tmp_1001"], late);
        Assert.Equal(["tmp_1002"], early);
        Assert.Equal([4.0, 8.0], _rig.Project.TempoMap.Select(change => change.Time));
        Assert.Equal(["tmp_1002", "tmp_1001"], _rig.Project.TempoMap.Select(change => change.Id.Value));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(0.005)]
    [InlineData(-3)]
    public void ATempoChangeAtTheStartIsRefusedBecauseThatIsTheProjectTempo(double time)
    {
        _rig.RefusedWith(new AddTempoChange { Project = _rig.Id, Time = time, Bpm = 90 }, CommandErrorCode.Invalid);
    }

    [Fact]
    public void ATempoChangeOnTopOfAnotherIsRefused()
    {
        _rig.Ok(new AddTempoChange { Project = _rig.Id, Time = 4, Bpm = 60 });

        _rig.RefusedWith(new AddTempoChange { Project = _rig.Id, Time = 4.0005, Bpm = 90 }, CommandErrorCode.Rejected);
        _rig.Ok(new AddTempoChange { Project = _rig.Id, Time = 4.002, Bpm = 90 });
    }

    [Fact]
    public void ATempoChangeWithAnImpossibleTempoIsRefusedAndNoIdIsReported()
    {
        CommandResult result = _rig.Refused(new AddTempoChange { Project = _rig.Id, Time = 4, Bpm = 0 });

        Assert.Empty(result.Created);
    }

    [Fact]
    public void ATempoChangeCanBeRetunedMovedAndRemoved()
    {
        _rig.Ok(new AddTempoChange { Project = _rig.Id, Time = 4, Bpm = 60 });
        _rig.Ok(new AddTempoChange { Project = _rig.Id, Time = 8, Bpm = 90 });
        TempoChangeId first = _rig.Project.TempoMap[0].Id;

        _rig.Ok(new SetTempoChangeBpm { Project = _rig.Id, Change = first, Bpm = 70 });
        Assert.Equal(70, _rig.Project.TempoMap[0].Bpm);

        _rig.Ok(new MoveTempoChange { Project = _rig.Id, Change = first, Time = 12 });
        Assert.Equal(first, _rig.Project.TempoMap[1].Id);
        Assert.Equal(12, _rig.Project.TempoMap[1].Time);

        _rig.Ok(new RemoveTempoChange { Project = _rig.Id, Change = first });
        Assert.Single(_rig.Project.TempoMap);
    }

    [Fact]
    public void MovingATempoChangeToTheStartStopsJustAfterIt()
    {
        _rig.Ok(new AddTempoChange { Project = _rig.Id, Time = 4, Bpm = 60 });

        _rig.Ok(new MoveTempoChange { Project = _rig.Id, Change = _rig.Project.TempoMap[0].Id, Time = -5 });

        Assert.Equal(0.01, _rig.Project.TempoMap[0].Time);
    }

    [Fact]
    public void MovingATempoChangeOntoAnotherOrByNothingIsHandledQuietly()
    {
        _rig.Ok(new AddTempoChange { Project = _rig.Id, Time = 4, Bpm = 60 });
        _rig.Ok(new AddTempoChange { Project = _rig.Id, Time = 8, Bpm = 90 });
        TempoChangeId first = _rig.Project.TempoMap[0].Id;
        int steps = _rig.History.UndoCount;

        _rig.RefusedWith(new MoveTempoChange { Project = _rig.Id, Change = first, Time = 8.0004 }, CommandErrorCode.Rejected);
        _rig.Ok(new MoveTempoChange { Project = _rig.Id, Change = first, Time = 4.00001 });

        Assert.Equal(steps, _rig.History.UndoCount);
    }

    [Fact]
    public void AnUnknownTempoChangeIsNotFound()
    {
        TempoChangeId none = TempoChangeId.From("tmp_none");

        _rig.RefusedWith(new SetTempoChangeBpm { Project = _rig.Id, Change = none, Bpm = 90 }, CommandErrorCode.NotFound);
        _rig.RefusedWith(new MoveTempoChange { Project = _rig.Id, Change = none, Time = 5 }, CommandErrorCode.NotFound);
        _rig.RefusedWith(new RemoveTempoChange { Project = _rig.Id, Change = none }, CommandErrorCode.NotFound);
    }

    [Fact]
    public void ASetBpmThatChangesNothingAddsNoUndoStepAndKeepsRedo()
    {
        _rig.Ok(new SetTempoBpm { Project = _rig.Id, Bpm = 100 });
        _rig.History.Undo();

        _rig.Ok(new SetTempoBpm { Project = _rig.Id, Bpm = 120 });

        Assert.Equal(0, _rig.History.UndoCount);
        Assert.Equal(1, _rig.History.RedoCount);
    }

    [Fact]
    public void ARedoneTempoChangeKeepsItsId()
    {
        _rig.Ok(new AddTempoChange { Project = _rig.Id, Time = 4, Bpm = 60 });
        TempoChangeId id = _rig.Project.TempoMap[0].Id;

        _rig.History.Undo();
        _rig.History.Redo();

        Assert.Equal(id, _rig.Project.TempoMap[0].Id);
    }
}
