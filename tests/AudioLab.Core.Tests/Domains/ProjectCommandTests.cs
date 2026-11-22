using AudioLab.Core.Commands;
using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Core.Tests;

public sealed class ProjectCommandTests
{
    private readonly CommandRig _rig = new();

    // ----- name and metronome

    [Fact]
    public void ARenameTrimsTheNameAndABlankOrOverlongOneIsRefusedWithTheRightClass()
    {
        _rig.Ok(new RenameProject { Project = _rig.Id, Name = "  My song  " });

        Assert.Equal("My song", _rig.Project.Name);
        _rig.RefusedWith(new RenameProject { Project = _rig.Id, Name = "   " }, CommandErrorCode.Rejected);
        _rig.RefusedWith(new RenameProject { Project = _rig.Id, Name = new string('n', ProjectLimits.MaxName + 1) }, CommandErrorCode.Invalid);
        _rig.RefusedWith(new RenameProject { Project = _rig.Id, Name = "bell\u0007" }, CommandErrorCode.Invalid);
        _rig.Ok(new RenameProject { Project = _rig.Id, Name = new string('n', ProjectLimits.MaxName) });
    }

    [Fact]
    public void TheMetronomeIsSwitchedAndSwitchingItToWhatItAlreadyIsAddsNoUndoStep()
    {
        _rig.Ok(new SetMetronome { Project = _rig.Id, Enabled = true });
        int steps = _rig.History.UndoCount;

        Assert.True(_rig.Run(new SetMetronome { Project = _rig.Id, Enabled = true }).Succeeded);

        Assert.True(_rig.Project.Metronome);
        Assert.Equal(steps, _rig.History.UndoCount);
        _rig.Ok(new SetMetronome { Project = _rig.Id, Enabled = false });
        Assert.False(_rig.Project.Metronome);
    }

    // ----- view

    [Fact]
    public void EachPartOfTheViewIsSetAloneAndTheOthersKeepTheirValue()
    {
        _rig.Ok(new SetProjectView { Project = _rig.Id, Ruler = RulerMode.Time });
        Assert.Equal((RulerMode.Time, EditMode.Slip, "1/4"), (_rig.Project.View.Ruler, _rig.Project.View.EditMode, _rig.Project.View.Grid));
        _rig.Ok(new SetProjectView { Project = _rig.Id, EditMode = EditMode.Shuffle });
        _rig.Ok(new SetProjectView { Project = _rig.Id, Grid = "1/16t" });

        Assert.Equal((RulerMode.Time, EditMode.Shuffle, "1/16t"), (_rig.Project.View.Ruler, _rig.Project.View.EditMode, _rig.Project.View.Grid));
        _rig.Ok(new SetProjectView { Project = _rig.Id, Ruler = RulerMode.Beats, EditMode = EditMode.Grid, Grid = "bar" });
        Assert.Equal((RulerMode.Beats, EditMode.Grid, "bar"), (_rig.Project.View.Ruler, _rig.Project.View.EditMode, _rig.Project.View.Grid));
    }

    [Fact]
    public void EveryGridValueTheEditorOffersIsAcceptedAndAnyOtherIsRefused()
    {
        foreach (string grid in GridRules.Values.Reverse())
        {
            CommandRig rig = new(_rig.Project with { View = _rig.Project.View with { Grid = "other" } });
            rig.Ok(new SetProjectView { Project = rig.Id, Grid = grid });
            Assert.Equal(grid, rig.Project.View.Grid);
        }

        _rig.RefusedWith(new SetProjectView { Project = _rig.Id, Grid = "1/7" }, CommandErrorCode.Invalid);
        _rig.RefusedWith(new SetProjectView { Project = _rig.Id, Grid = string.Empty }, CommandErrorCode.Invalid);
        _rig.RefusedWith(new SetProjectView { Project = _rig.Id, Grid = "1S" }, CommandErrorCode.Invalid);
    }

    [Fact]
    public void ASetViewWithNothingToSetIsRefusedAndOneThatChangesNothingAddsNoUndoStep()
    {
        _rig.RefusedWith(new SetProjectView { Project = _rig.Id }, CommandErrorCode.Invalid);
        int steps = _rig.History.UndoCount;

        Assert.True(_rig.Run(new SetProjectView { Project = _rig.Id, Grid = "1/4", Ruler = RulerMode.Beats }).Succeeded);

        Assert.Equal(steps, _rig.History.UndoCount);
        _rig.RefusedWith(new SetProjectView { Project = _rig.Id, Ruler = (RulerMode)9 }, CommandErrorCode.Invalid);
    }
}
