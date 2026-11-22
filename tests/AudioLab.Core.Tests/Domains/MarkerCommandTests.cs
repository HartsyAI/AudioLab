using AudioLab.Core.Commands;
using AudioLab.Core.Model;

namespace AudioLab.Core.Tests;

public sealed class MarkerCommandTests
{
    private readonly CommandRig _rig = new();

    [Fact]
    public void AMarkerIsAddedInTimeOrderWithADefaultNameAndAServerAllocatedId()
    {
        IReadOnlyList<string> late = _rig.Created(new AddMarker { Project = _rig.Id, Time = 30 });
        _rig.Created(new AddMarker { Project = _rig.Id, Time = 10, Name = "  Verse " });
        _rig.Created(new AddMarker { Project = _rig.Id, Time = 20, Name = " " });

        Assert.Equal(["mrk_1001"], late);
        Assert.Equal([10.0, 20.0, 30.0], _rig.Project.Markers.Select(marker => marker.Time));
        Assert.Equal(["Verse", "Marker 3", "Marker 1"], _rig.Project.Markers.Select(marker => marker.Name));
    }

    [Fact]
    public void AMarkerBeforeTheStartIsRefusedWhenAddedAndStopsAtTheStartWhenMoved()
    {
        _rig.RefusedWith(new AddMarker { Project = _rig.Id, Time = -4 }, CommandErrorCode.Rejected);
        _rig.Ok(new AddMarker { Project = _rig.Id, Time = 5 });

        _rig.Ok(new MoveMarker { Project = _rig.Id, Marker = _rig.Project.Markers[0].Id, Time = -4 });

        Assert.Equal(0, _rig.Project.Markers[0].Time);
    }

    [Fact]
    public void AMarkerOnTopOfAnotherIsRefused()
    {
        _rig.Ok(new AddMarker { Project = _rig.Id, Time = 5 });

        _rig.RefusedWith(new AddMarker { Project = _rig.Id, Time = 5.0004 }, CommandErrorCode.Rejected);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(2_000_000)]
    public void AMarkerAtATimeTheProjectCannotHoldIsRefused(double time)
    {
        _rig.Refused(new AddMarker { Project = _rig.Id, Time = time });
    }

    [Fact]
    public void RenamingTrimsAndRefusesABlankName()
    {
        _rig.Ok(new AddMarker { Project = _rig.Id, Time = 5 });
        MarkerId id = _rig.Project.Markers[0].Id;

        _rig.Ok(new RenameMarker { Project = _rig.Id, Marker = id, Name = "  Drop " });
        Assert.Equal("Drop", _rig.Project.Markers[0].Name);
        _rig.RefusedWith(new RenameMarker { Project = _rig.Id, Marker = id, Name = "   " }, CommandErrorCode.Rejected);
    }

    [Fact]
    public void RenamingToTheSameNameAddsNoUndoStep()
    {
        _rig.Ok(new AddMarker { Project = _rig.Id, Time = 5, Name = "Drop" });
        int steps = _rig.History.UndoCount;

        _rig.Ok(new RenameMarker { Project = _rig.Id, Marker = _rig.Project.Markers[0].Id, Name = "Drop" });

        Assert.Equal(steps, _rig.History.UndoCount);
    }

    [Fact]
    public void MovingAMarkerKeepsTheListInTimeOrder()
    {
        _rig.Ok(new AddMarker { Project = _rig.Id, Time = 5, Name = "A" });
        _rig.Ok(new AddMarker { Project = _rig.Id, Time = 10, Name = "B" });

        _rig.Ok(new MoveMarker { Project = _rig.Id, Marker = _rig.Project.Markers[0].Id, Time = 20 });

        Assert.Equal(["B", "A"], _rig.Project.Markers.Select(marker => marker.Name));
        _rig.History.Undo();
        Assert.Equal(["A", "B"], _rig.Project.Markers.Select(marker => marker.Name));
    }

    [Fact]
    public void MovingAMarkerOntoAnotherIsRefusedAndMovingItByNothingIsNoStep()
    {
        _rig.Ok(new AddMarker { Project = _rig.Id, Time = 5 });
        _rig.Ok(new AddMarker { Project = _rig.Id, Time = 10 });
        MarkerId first = _rig.Project.Markers[0].Id;
        int steps = _rig.History.UndoCount;

        _rig.RefusedWith(new MoveMarker { Project = _rig.Id, Marker = first, Time = 10.0004 }, CommandErrorCode.Rejected);
        _rig.Ok(new MoveMarker { Project = _rig.Id, Marker = first, Time = 5 });

        Assert.Equal(steps, _rig.History.UndoCount);
    }

    [Fact]
    public void AMarkerColourIsSetClearedAndChecked()
    {
        _rig.Ok(new AddMarker { Project = _rig.Id, Time = 5 });
        MarkerId id = _rig.Project.Markers[0].Id;

        _rig.Ok(new SetMarkerColor { Project = _rig.Id, Marker = id, Color = "#f4a261" });
        Assert.Equal("#f4a261", _rig.Project.Markers[0].Color);
        _rig.RefusedWith(new SetMarkerColor { Project = _rig.Id, Marker = id, Color = "red; background: url(x)" }, CommandErrorCode.Invalid);
        _rig.Ok(new SetMarkerColor { Project = _rig.Id, Marker = id });
        Assert.Null(_rig.Project.Markers[0].Color);
    }

    [Fact]
    public void AnUnknownMarkerIsNotFound()
    {
        MarkerId none = MarkerId.From("mrk_none");

        _rig.RefusedWith(new RenameMarker { Project = _rig.Id, Marker = none, Name = "x" }, CommandErrorCode.NotFound);
        _rig.RefusedWith(new MoveMarker { Project = _rig.Id, Marker = none, Time = 1 }, CommandErrorCode.NotFound);
        _rig.RefusedWith(new SetMarkerColor { Project = _rig.Id, Marker = none }, CommandErrorCode.NotFound);
        _rig.RefusedWith(new RemoveMarker { Project = _rig.Id, Marker = none }, CommandErrorCode.NotFound);
    }

    [Fact]
    public void ARemovedMarkerComesBackWithItsIdOnUndo()
    {
        _rig.Ok(new AddMarker { Project = _rig.Id, Time = 5, Name = "A" });
        Marker marker = _rig.Project.Markers[0];

        _rig.Ok(new RemoveMarker { Project = _rig.Id, Marker = marker.Id });
        Assert.Empty(_rig.Project.Markers);
        _rig.History.Undo();

        Assert.Equal(marker, _rig.Project.Markers[0]);
    }
}
