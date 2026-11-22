using AudioLab.Core.Commands;
using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Core.Tests;

public sealed class SectionCommandTests
{
    private readonly CommandRig _rig = new();

    [Fact]
    public void ANewSectionRunsFourBarsAtTheTempoThere()
    {
        _rig.Created(new AddSection { Project = _rig.Id, Start = 10 });

        Section section = _rig.Project.Sections[0];
        Assert.Equal(10, section.Start);
        Assert.Equal(18, section.End);
        Assert.Equal("Section 1", section.Name);
        Assert.Equal(ProjectDefaults.SectionColor(0), section.Color);
    }

    [Fact]
    public void TheDefaultLengthFollowsTheMeterAndTheTempoChanges()
    {
        _rig.Ok(new SetTimeSignature { Project = _rig.Id, Numerator = 6, Denominator = 8 });
        _rig.Created(new AddSection { Project = _rig.Id, Start = 0 });
        _rig.Ok(new AddTempoChange { Project = _rig.Id, Time = 100, Bpm = 60 });
        _rig.Created(new AddSection { Project = _rig.Id, Start = 100 });

        Assert.Equal(6, _rig.Project.Sections[0].End);
        Assert.Equal(112, _rig.Project.Sections[1].End);
    }

    [Fact]
    public void ASectionIsCutShortAtTheNextAndMayNotStartInsideAnother()
    {
        _rig.Ok(new AddSection { Project = _rig.Id, Start = 20, Length = 10 });

        _rig.Ok(new AddSection { Project = _rig.Id, Start = 15, Length = 50, Name = " Intro ", Color = "#112233" });

        Assert.Equal(["Intro", "Section 1"], _rig.Project.Sections.Select(section => section.Name));
        Assert.Equal(20, _rig.Project.Sections[0].End);
        _rig.RefusedWith(new AddSection { Project = _rig.Id, Start = 22 }, CommandErrorCode.Rejected);
    }

    [Fact]
    public void ASectionNeedsRoomFortheMinimumLength()
    {
        _rig.Ok(new AddSection { Project = _rig.Id, Start = 0, Length = 10 });
        _rig.Ok(new AddSection { Project = _rig.Id, Start = 10.05, Length = 5 });

        _rig.RefusedWith(new AddSection { Project = _rig.Id, Start = 10 }, CommandErrorCode.Rejected);
    }

    [Fact]
    public void ADefaultNameSkipsOneAlreadyTaken()
    {
        _rig.Ok(new AddSection { Project = _rig.Id, Start = 0, Length = 5, Name = "Section 2" });
        _rig.Ok(new AddSection { Project = _rig.Id, Start = 10, Length = 5 });

        Assert.Equal("Section 3", _rig.Project.Sections[1].Name);
    }

    [Fact]
    public void ADraggedSectionStopsAtItsNeighbourAndUndoRestoresIt()
    {
        _rig.Ok(new AddSection { Project = _rig.Id, Start = 0, Length = 10 });
        _rig.Ok(new AddSection { Project = _rig.Id, Start = 20, Length = 10 });
        SectionId first = _rig.Project.Sections[0].Id;
        ProjectDocument before = _rig.Project;

        _rig.Ok(new SetSectionSpan { Project = _rig.Id, Section = first, Start = 15, End = 0, Edit = SectionEdit.Move });

        Assert.Equal(10, _rig.Project.Sections[0].Start);
        Assert.Equal(20, _rig.Project.Sections[0].End);
        _rig.History.Undo();
        Assert.Equal(before, _rig.Project);
    }

    [Fact]
    public void AnEdgeIsDraggedAndStopsAtTheMinimumLength()
    {
        _rig.Ok(new AddSection { Project = _rig.Id, Start = 10, Length = 10 });
        SectionId id = _rig.Project.Sections[0].Id;

        _rig.Ok(new SetSectionSpan { Project = _rig.Id, Section = id, Start = 4, End = 0, Edit = SectionEdit.Start });
        Assert.Equal(4, _rig.Project.Sections[0].Start);
        _rig.Ok(new SetSectionSpan { Project = _rig.Id, Section = id, Start = 0, End = 0, Edit = SectionEdit.End });

        Assert.Equal(4.1, _rig.Project.Sections[0].End, 9);
    }

    [Fact]
    public void ADragGestureIsOneUndoStep()
    {
        _rig.Ok(new AddSection { Project = _rig.Id, Start = 0, Length = 10 });
        SectionId id = _rig.Project.Sections[0].Id;
        GestureId drag = GestureId.From("drag");

        for (int i = 1; i <= 5; i++)
        {
            _rig.Run(new SetSectionSpan { Project = _rig.Id, Section = id, Start = i, End = 0, Edit = SectionEdit.Move }, drag);
        }
        _rig.History.Undo();

        Assert.Equal(0, _rig.Project.Sections[0].Start);
        Assert.Equal(1, _rig.History.UndoCount);
    }

    [Fact]
    public void ASectionRenamedRecolouredAndRemovedKeepsTheRules()
    {
        _rig.Ok(new AddSection { Project = _rig.Id, Start = 0, Length = 10 });
        SectionId id = _rig.Project.Sections[0].Id;

        _rig.Ok(new RenameSection { Project = _rig.Id, Section = id, Name = " Drop " });
        _rig.RefusedWith(new RenameSection { Project = _rig.Id, Section = id, Name = "" }, CommandErrorCode.Rejected);
        _rig.Ok(new SetSectionColor { Project = _rig.Id, Section = id, Color = "#abc" });
        _rig.RefusedWith(new SetSectionColor { Project = _rig.Id, Section = id, Color = "blue" }, CommandErrorCode.Invalid);
        Assert.Equal(("Drop", "#abc"), (_rig.Project.Sections[0].Name, _rig.Project.Sections[0].Color));

        _rig.Ok(new RemoveSection { Project = _rig.Id, Section = id });
        Assert.Empty(_rig.Project.Sections);
    }

    [Fact]
    public void AnUnknownSectionIsNotFound()
    {
        SectionId none = SectionId.From("sec_none");

        _rig.RefusedWith(new SetSectionSpan { Project = _rig.Id, Section = none, Start = 1, End = 2, Edit = SectionEdit.Move }, CommandErrorCode.NotFound);
        _rig.RefusedWith(new RenameSection { Project = _rig.Id, Section = none, Name = "x" }, CommandErrorCode.NotFound);
        _rig.RefusedWith(new SetSectionColor { Project = _rig.Id, Section = none, Color = "#fff" }, CommandErrorCode.NotFound);
        _rig.RefusedWith(new RemoveSection { Project = _rig.Id, Section = none }, CommandErrorCode.NotFound);
    }

    [Fact]
    public void ClearingSectionsIsOneUndoStepAndNothingWhenThereAreNone()
    {
        _rig.Ok(new ClearSections { Project = _rig.Id });
        Assert.Equal(0, _rig.History.UndoCount);
        _rig.Ok(new AddSection { Project = _rig.Id, Start = 0, Length = 10 });
        _rig.Ok(new AddSection { Project = _rig.Id, Start = 20, Length = 10 });

        _rig.Ok(new ClearSections { Project = _rig.Id });
        Assert.Empty(_rig.Project.Sections);
        _rig.History.Undo();

        Assert.Equal(2, _rig.Project.Sections.Count);
    }

    [Fact]
    public void SectionsFromMarkersReplaceTheExistingOnesAndAllocateIdsInOrder()
    {
        _rig.Ok(new AddSection { Project = _rig.Id, Start = 0, Length = 3, Name = "Old" });
        _rig.Ok(new AddMarker { Project = _rig.Id, Time = 4, Name = "Verse" });
        _rig.Ok(new AddMarker { Project = _rig.Id, Time = 20, Name = "Chorus" });
        _rig.Ok(new SetMarkerColor { Project = _rig.Id, Marker = _rig.Project.Markers[1].Id, Color = "#123456" });
        ProjectDocument before = _rig.Project;

        IReadOnlyList<string> created = _rig.Created(new SectionsFromMarkers { Project = _rig.Id });

        Assert.Equal(created, _rig.Project.Sections.Select(section => section.Id.Value));
        Assert.Equal(["Verse", "Chorus"], _rig.Project.Sections.Select(section => section.Name));
        Assert.Equal(4, _rig.Project.Sections[0].Start);
        Assert.Equal(20, _rig.Project.Sections[0].End);
        Assert.Equal("#123456", _rig.Project.Sections[1].Color);
        _rig.History.Undo();
        Assert.Equal(before, _rig.Project);
    }

    [Fact]
    public void TheLastSectionFromMarkersRunsToTheEndOfTheContentOrFourBarsPastTheLastMarker()
    {
        _rig.Ok(new AddMarker { Project = _rig.Id, Time = 4 });
        _rig.Ok(new SectionsFromMarkers { Project = _rig.Id });
        Assert.Equal(12, _rig.Project.Sections[0].End);

        CommandRig withClip = new(BlankProject.Create() with { Tracks = [BlankProject.Track("trk_1") with { Clips = [Clip(0, 50)] }] });
        withClip.Ok(new AddMarker { Project = withClip.Id, Time = 4 });
        withClip.Ok(new SectionsFromMarkers { Project = withClip.Id });
        Assert.Equal(50, withClip.Project.Sections[0].End);
    }

    [Fact]
    public void SectionsFromMarkersNeedMarkers()
    {
        _rig.RefusedWith(new SectionsFromMarkers { Project = _rig.Id }, CommandErrorCode.Rejected);
    }

    private static AudioClip Clip(double start, double duration)
    {
        return new AudioClip
        {
            Id = ClipId.From("clp_1"),
            Name = "a",
            Media = MediaId.From("med_1"),
            Start = start,
            Duration = duration,
            Offset = 0,
            TrimEnd = 0,
            Gain = 1,
            FadeIn = 0,
            FadeOut = 0,
            Muted = false,
        };
    }
}
