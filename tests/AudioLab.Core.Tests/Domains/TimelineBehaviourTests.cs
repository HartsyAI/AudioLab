using AudioLab.Core.Commands;
using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Core.Tests;

// What the tempo does to the rest of the project, and how the timeline commands behave as one drag.
public sealed class TimelineBehaviourTests
{
    private readonly CommandRig _rig = new();

    private ProjectDocument Placed()
    {
        _rig.Ok(new AddMarker { Project = _rig.Id, Time = 5, Name = "A" });
        _rig.Ok(new AddSection { Project = _rig.Id, Start = 10, Length = 8 });
        AudioClip clip = new()
        {
            Id = ClipId.From("clp_1"),
            Name = "c",
            Media = MediaId.From("med_1"),
            Start = 3,
            Duration = 12,
            Offset = 0,
            TrimEnd = 0,
            Gain = 1,
            FadeIn = 0,
            FadeOut = 0,
            Muted = false,
        };
        ProjectDocument withClip = _rig.Project with { Tracks = [BlankProject.Track("trk_1") with { Clips = [clip] }] };
        return withClip;
    }

    // Everything placed in seconds: markers, sections and the clips on every track.
    private static (ValueList<Marker>, ValueList<Section>, ValueList<Track>) InSeconds(ProjectDocument document)
    {
        return (document.Markers, document.Sections, document.Tracks);
    }

    [Fact]
    public void ThingsPlacedInSecondsStayPutWhenTheTempoOrTheMeterChanges()
    {
        CommandRig rig = new(Placed());
        (ValueList<Marker>, ValueList<Section>, ValueList<Track>) before = InSeconds(rig.Project);

        rig.Ok(new SetTempoBpm { Project = rig.Id, Bpm = 90 });
        Assert.Equal(before, InSeconds(rig.Project));
        rig.Ok(new SetTimeSignature { Project = rig.Id, Numerator = 7, Denominator = 8 });
        Assert.Equal(before, InSeconds(rig.Project));
        string change = rig.Created(new AddTempoChange { Project = rig.Id, Time = 6, Bpm = 200 })[0];
        Assert.Equal(before, InSeconds(rig.Project));
        rig.Ok(new SetTempoChangeBpm { Project = rig.Id, Change = TempoChangeId.From(change), Bpm = 60 });
        rig.Ok(new MoveTempoChange { Project = rig.Id, Change = TempoChangeId.From(change), Time = 12 });
        Assert.Equal(before, InSeconds(rig.Project));
        rig.Ok(new RemoveTempoChange { Project = rig.Id, Change = TempoChangeId.From(change) });
        Assert.Equal(before, InSeconds(rig.Project));
    }

    [Fact]
    public void OnlyTheBarsAndBeatsMoveWhenTheTempoChanges()
    {
        TempoMap before = TempoMap.From(_rig.Project);

        _rig.Ok(new SetTempoBpm { Project = _rig.Id, Bpm = 60 });
        TempoMap after = TempoMap.From(_rig.Project);

        Assert.Equal(20, before.BeatsAt(10));
        Assert.Equal(10, after.BeatsAt(10));
    }

    [Fact]
    public void DraggingAMarkerIsOneGestureAndEveryFrameSucceeds()
    {
        _rig.Ok(new AddMarker { Project = _rig.Id, Time = 5, Name = "A" });
        _rig.Ok(new AddMarker { Project = _rig.Id, Time = 40, Name = "B" });
        MarkerId id = _rig.Project.Markers[0].Id;
        GestureId drag = GestureId.From("marker-drag");
        int steps = _rig.History.UndoCount;

        foreach (double time in new[] { 6.0, 9.0, 15.0, 22.0 })
        {
            Assert.True(_rig.Run(new MoveMarker { Project = _rig.Id, Marker = id, Time = time }, drag).Succeeded, $"frame at {time}");
        }
        _rig.History.EndGesture(ClientId.From("test-client"), drag);

        Assert.Equal(steps + 1, _rig.History.UndoCount);
        Assert.Equal(22, _rig.Project.Markers[0].Time);
        _rig.History.Undo();
        Assert.Equal(5, _rig.Project.Markers[0].Time);
    }

    [Fact]
    public void ADragFrameOntoAnotherMarkerIsRefusedAndTheGestureGoesOn()
    {
        _rig.Ok(new AddMarker { Project = _rig.Id, Time = 5 });
        _rig.Ok(new AddMarker { Project = _rig.Id, Time = 20 });
        MarkerId id = _rig.Project.Markers[0].Id;
        GestureId drag = GestureId.From("marker-drag");

        Assert.True(_rig.Run(new MoveMarker { Project = _rig.Id, Marker = id, Time = 10 }, drag).Succeeded);
        Assert.False(_rig.Run(new MoveMarker { Project = _rig.Id, Marker = id, Time = 20 }, drag).Succeeded);
        Assert.True(_rig.Run(new MoveMarker { Project = _rig.Id, Marker = id, Time = 12 }, drag).Succeeded);

        Assert.Equal(12, _rig.Project.Markers[0].Time);
        _rig.History.EndGesture(ClientId.From("test-client"), drag);
        _rig.History.Undo();
        Assert.Equal(5, _rig.Project.Markers[0].Time);
    }

    [Fact]
    public void DraggingATempoChangeIsOneGesture()
    {
        _rig.Ok(new AddTempoChange { Project = _rig.Id, Time = 4, Bpm = 60 });
        TempoChangeId id = _rig.Project.TempoMap[0].Id;
        GestureId drag = GestureId.From("tempo-drag");
        int steps = _rig.History.UndoCount;

        foreach (double time in new[] { 5.0, 7.5, 9.0 })
        {
            Assert.True(_rig.Run(new MoveTempoChange { Project = _rig.Id, Change = id, Time = time }, drag).Succeeded, $"frame at {time}");
        }
        _rig.History.EndGesture(ClientId.From("test-client"), drag);

        Assert.Equal(steps + 1, _rig.History.UndoCount);
        _rig.History.Undo();
        Assert.Equal(4, _rig.Project.TempoMap[0].Time);
    }

    [Fact]
    public void ResizingASectionEdgeIsOneGesture()
    {
        _rig.Ok(new AddSection { Project = _rig.Id, Start = 10, Length = 10 });
        SectionId id = _rig.Project.Sections[0].Id;
        GestureId drag = GestureId.From("edge-drag");
        int steps = _rig.History.UndoCount;

        foreach (double end in new[] { 22.0, 25.0, 31.0, 40.0 })
        {
            Assert.True(_rig.Run(new SetSectionSpan { Project = _rig.Id, Section = id, End = end, Edit = SectionEdit.End }, drag).Succeeded, $"end {end}");
        }
        _rig.History.EndGesture(ClientId.From("test-client"), drag);

        Assert.Equal(steps + 1, _rig.History.UndoCount);
        Assert.Equal(40, _rig.Project.Sections[0].End);
        _rig.History.Undo();
        Assert.Equal(20, _rig.Project.Sections[0].End);
    }

    [Fact]
    public void ABlockedOrUnchangedSectionDragAddsNoUndoStep()
    {
        _rig.Ok(new AddSection { Project = _rig.Id, Start = 0, Length = 10 });
        _rig.Ok(new AddSection { Project = _rig.Id, Start = 10, Length = 10 });
        SectionId second = _rig.Project.Sections[1].Id;
        int steps = _rig.History.UndoCount;

        _rig.Ok(new SetSectionSpan { Project = _rig.Id, Section = second, Start = 4, Edit = SectionEdit.Move });
        _rig.Ok(new SetSectionSpan { Project = _rig.Id, Section = second, Start = 2, Edit = SectionEdit.Start });
        _rig.Ok(new SetSectionSpan { Project = _rig.Id, Section = second, Start = 10, Edit = SectionEdit.Move });

        Assert.Equal(steps, _rig.History.UndoCount);
        Assert.Equal(new SectionSpan(10, 20), new SectionSpan(_rig.Project.Sections[1].Start, _rig.Project.Sections[1].End));
    }

    [Fact]
    public void ASectionEditNeedsTheValueItUses()
    {
        _rig.Ok(new AddSection { Project = _rig.Id, Start = 10, Length = 10 });
        SectionId id = _rig.Project.Sections[0].Id;

        _rig.RefusedWith(new SetSectionSpan { Project = _rig.Id, Section = id, Edit = SectionEdit.Move }, CommandErrorCode.Rejected);
        _rig.RefusedWith(new SetSectionSpan { Project = _rig.Id, Section = id, Start = 4, Edit = SectionEdit.End }, CommandErrorCode.Rejected);
        _rig.Ok(new SetSectionSpan { Project = _rig.Id, Section = id, End = 30, Edit = SectionEdit.End });
        _rig.RefusedWith(new SetSectionSpan { Project = _rig.Id, Section = id, Start = double.NaN, Edit = SectionEdit.Start }, CommandErrorCode.Invalid);
    }

    [Fact]
    public void SectionsFromMarkersMakeNewIdsEveryTimeTheyRun()
    {
        _rig.Ok(new AddMarker { Project = _rig.Id, Time = 4 });
        _rig.Ok(new AddMarker { Project = _rig.Id, Time = 20 });

        IReadOnlyList<string> first = _rig.Created(new SectionsFromMarkers { Project = _rig.Id });
        IReadOnlyList<string> second = _rig.Created(new SectionsFromMarkers { Project = _rig.Id });

        Assert.Equal(2, first.Count);
        Assert.Empty(first.Intersect(second));
        Assert.Equal(second, _rig.Project.Sections.Select(section => section.Id.Value));
    }

    [Fact]
    public void AnOutOfRangeValueIsTheSameClassOfErrorWhoeverFindsIt()
    {
        CommandErrorCode own = _rig.Refused(new AddMarker { Project = _rig.Id, Time = -1 }).Errors[0].Code;
        CommandErrorCode validator = _rig.Refused(new SetTempoBpm { Project = _rig.Id, Bpm = 5000 }).Errors[0].Code;
        CommandErrorCode nonFinite = _rig.Refused(new AddSection { Project = _rig.Id, Start = double.NaN }).Errors[0].Code;

        Assert.Equal(CommandErrorCode.Invalid, own);
        Assert.Equal(own, validator);
        Assert.Equal(own, nonFinite);
    }
}
