using AudioLab.Core.Commands;
using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Core.Tests;

public sealed class TrackCommandTests
{
    private readonly CommandRig _rig = new();

    private TrackId Add(TrackKind? kind = null, string? name = null, string? preset = null)
    {
        return TrackId.From(_rig.Created(new AddTrack { Project = _rig.Id, Kind = kind, Name = name, Preset = preset })[0]);
    }

    [Fact]
    public void AnAudioTrackStartsWithTheOldDawsValuesAndAServerAllocatedId()
    {
        TrackId id = Add();

        Track track = _rig.Project.Tracks[0];
        Assert.Equal("trk_1001", id.Value);
        Assert.Equal(("Track 1", TrackKind.Audio, ProjectDefaults.TrackColor(0)), (track.Name, track.Kind, track.Color));
        Assert.Equal((0.8, 0.0, 100.0), (track.Volume, track.Pan, track.View.Height));
        Assert.Null(track.Instrument);
    }

    [Fact]
    public void ABusStartsAtUnityAndAnInstrumentTrackWithItsSound()
    {
        Add(TrackKind.Bus);
        Add(TrackKind.Instrument);
        Add(TrackKind.Instrument, preset: "Bell");
        Add(TrackKind.Instrument, preset: "Sampler");

        Track[] tracks = [.. _rig.Project.Tracks];
        Assert.Equal(("Bus 1", 1.0), (tracks[0].Name, tracks[0].Volume));
        Assert.Equal("Soft Keys 1", tracks[1].Name);
        Assert.Equal("Soft Keys", Assert.IsType<SynthInstrument>(tracks[1].Instrument).Preset);
        Assert.Equal(("Bell 1", "Bell"), (tracks[2].Name, Assert.IsType<SynthInstrument>(tracks[2].Instrument).Preset));
        Assert.Equal("Sampler 1", tracks[3].Name);
        Assert.IsType<SamplerInstrument>(tracks[3].Instrument);
    }

    [Fact]
    public void ADefaultNameTakesTheLowestFreeNumberAndColoursCycleThroughTheTokens()
    {
        TrackId first = Add();
        Add();
        Add();
        _rig.Ok(new RemoveTrack { Project = _rig.Id, Track = first });

        Add();

        Assert.Equal(["Track 2", "Track 3", "Track 1"], _rig.Project.Tracks.Select(track => track.Name));
        Assert.Equal(ProjectDefaults.TrackColor(2), _rig.Project.Tracks[2].Color);
    }

    [Fact]
    public void ANameAndColourAreUsedAsGiven()
    {
        _rig.Ok(new AddTrack { Project = _rig.Id, Name = "  Lead vocal ", Color = "#123456" });

        Assert.Equal(("Lead vocal", "#123456"), (_rig.Project.Tracks[0].Name, _rig.Project.Tracks[0].Color));
    }

    [Fact]
    public void APresetBelongsToAnInstrumentTrackAndMustBeOneWeShip()
    {
        _rig.RefusedWith(new AddTrack { Project = _rig.Id, Preset = "Bell" }, CommandErrorCode.Rejected);
        _rig.RefusedWith(new AddTrack { Project = _rig.Id, Kind = TrackKind.Instrument, Preset = "Kazoo" }, CommandErrorCode.Invalid);
        _rig.RefusedWith(new AddTrack { Project = _rig.Id, Color = "red" }, CommandErrorCode.Invalid);
        _rig.RefusedWith(new AddTrack { Project = _rig.Id, Name = new string('n', 201) }, CommandErrorCode.Invalid);
    }

    [Fact]
    public void EveryPresetMakesAValidInstrumentTrack()
    {
        foreach (string preset in InstrumentPresets.Names)
        {
            Assert.True(_rig.Run(new AddTrack { Project = _rig.Id, Kind = TrackKind.Instrument, Preset = preset }).Succeeded, preset);
        }
        Assert.Equal(InstrumentPresets.Names.Count, _rig.Project.Tracks.Count);
    }

    [Fact]
    public void RemovingATrackTakesItsSendsAndGroupWithIt()
    {
        TrackId source = Add(name: "Source");
        TrackId other = Add(name: "Other");
        TrackId bus = Add(TrackKind.Bus);
        _rig.Ok(new AddSend { Project = _rig.Id, Track = source, Bus = bus });
        _rig.Ok(new CreateGroup { Project = _rig.Id, Tracks = [source] });
        ProjectDocument before = _rig.Project;

        _rig.Ok(new RemoveTrack { Project = _rig.Id, Track = bus });
        Assert.Empty(_rig.Project.Tracks.First(track => track.Id == source).Sends);
        _rig.Ok(new RemoveTrack { Project = _rig.Id, Track = source });

        Assert.Equal([other.Value], _rig.Project.Tracks.Select(track => track.Id.Value));
        Assert.Empty(_rig.Project.Groups);
        _rig.History.Undo();
        _rig.History.Undo();
        Assert.Equal(before, _rig.Project);
    }

    [Fact]
    public void ATrackIsRenamedAndRecoloured()
    {
        TrackId id = Add();

        _rig.Ok(new RenameTrack { Project = _rig.Id, Track = id, Name = " Drums " });
        _rig.Ok(new SetTrackColor { Project = _rig.Id, Track = id, Color = "#abc" });

        Assert.Equal(("Drums", "#abc"), (_rig.Project.Tracks[0].Name, _rig.Project.Tracks[0].Color));
        _rig.RefusedWith(new RenameTrack { Project = _rig.Id, Track = id, Name = "  " }, CommandErrorCode.Rejected);
        _rig.RefusedWith(new RenameTrack { Project = _rig.Id, Track = id, Name = new string('n', 201) }, CommandErrorCode.Invalid);
        _rig.RefusedWith(new SetTrackColor { Project = _rig.Id, Track = id, Color = "blue" }, CommandErrorCode.Invalid);
    }

    [Fact]
    public void ATrackStepsUpAndDownAndNothingHappensAtTheEnds()
    {
        TrackId a = Add(name: "A");
        Add(name: "B");
        Add(name: "C");
        int steps = _rig.History.UndoCount;

        _rig.Ok(new MoveTrack { Project = _rig.Id, Track = a, Direction = MoveDirection.Up });
        Assert.Equal(steps, _rig.History.UndoCount);
        _rig.Ok(new MoveTrack { Project = _rig.Id, Track = a, Direction = MoveDirection.Down });

        Assert.Equal(["B", "A", "C"], _rig.Project.Tracks.Select(track => track.Name));
        _rig.History.Undo();
        Assert.Equal(["A", "B", "C"], _rig.Project.Tracks.Select(track => track.Name));
    }

    [Fact]
    public void AnUnknownTrackIsNotFound()
    {
        TrackId none = TrackId.From("trk_none");

        _rig.RefusedWith(new RemoveTrack { Project = _rig.Id, Track = none }, CommandErrorCode.NotFound);
        _rig.RefusedWith(new RenameTrack { Project = _rig.Id, Track = none, Name = "x" }, CommandErrorCode.NotFound);
        _rig.RefusedWith(new SetTrackColor { Project = _rig.Id, Track = none, Color = "#fff" }, CommandErrorCode.NotFound);
        _rig.RefusedWith(new MoveTrack { Project = _rig.Id, Track = none, Direction = MoveDirection.Up }, CommandErrorCode.NotFound);
    }

    [Fact]
    public void ARedoneTrackKeepsItsId()
    {
        TrackId id = Add();

        _rig.History.Undo();
        _rig.History.Redo();

        Assert.Equal(id, _rig.Project.Tracks[0].Id);
    }
}
