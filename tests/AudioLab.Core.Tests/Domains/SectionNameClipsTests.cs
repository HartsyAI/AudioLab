using AudioLab.Core.Commands;
using AudioLab.Core.Model;

namespace AudioLab.Core.Tests;

public sealed class SectionNameClipsTests
{
    private readonly SectionId _intro = SectionId.From("sec_intro");

    private readonly SectionId _drop = SectionId.From("sec_drop");

    private readonly CommandRig _rig;

    public SectionNameClipsTests()
    {
        Track drums = BlankProject.Track("trk_drums", "Drums") with { Clips = [Clip("d1", 2), Clip("d2", 12), Clip("d3", 25)] };
        Instrument synth = InstrumentPresets.CreateSynth("Soft Keys");
        Track keys = ProjectDefaults.NewTrack(TrackId.From("trk_keys"), "Keys", TrackKind.Instrument, "#abc", synth) with
        {
            MidiClips =
            [
                new MidiClip
                {
                    Id = ClipId.From("m1"),
                    Name = "m1",
                    Start = 3,
                    Length = 2,
                    Muted = false,
                    Notes = [],
                    NextNoteId = new NoteId(1),
                    ControlChanges = [],
                },
            ],
        };
        _rig = new CommandRig(BlankProject.Create() with
        {
            Tracks = [drums, keys, BlankProject.Track("trk_bus", "Bus", TrackKind.Bus)],
            Sections =
            [
                new Section { Id = _intro, Name = "Intro", Start = 0, End = 10, Color = "#ff0000" },
                new Section { Id = _drop, Name = "Drop 1", Start = 10, End = 20, Color = "#00ff00" },
            ],
        });
    }

    private static AudioClip Clip(string id, double start)
    {
        return new AudioClip
        {
            Id = ClipId.From(id),
            Name = id,
            Media = MediaId.From("med_1"),
            Start = start,
            Duration = 2,
            Offset = 0,
            TrimEnd = 0,
            Gain = 1,
            FadeIn = 0,
            FadeOut = 0,
            Muted = false,
        };
    }

    private IEnumerable<string> Names
    {
        get
        {
            return _rig.Project.Tracks.SelectMany(track => track.Clips.Select(clip => clip.Name).Concat(track.MidiClips.Select(clip => clip.Name)));
        }
    }

    [Fact]
    public void EveryClipIsNamedAfterTheSectionItStartsInThenItsTrackAndOneOutsideEverySectionKeepsItsName()
    {
        _rig.Ok(new NameClipsAfterSections { Project = _rig.Id });

        Assert.Equal(["Intro - Drums", "Drop 1 - Drums", "d3", "Intro - Keys"], Names);
    }

    [Fact]
    public void WithASectionOnlyItsClipsAreRenamed()
    {
        _rig.Ok(new NameClipsAfterSections { Project = _rig.Id, Section = _drop });

        Assert.Equal(["d1", "Drop 1 - Drums", "d3", "m1"], Names);
    }

    [Fact]
    public void ASectionThatIsNotThereIsNotFoundAndRenamingAgainChangesNothing()
    {
        _rig.RefusedWith(new NameClipsAfterSections { Project = _rig.Id, Section = SectionId.From("sec_none") }, CommandErrorCode.NotFound);
        _rig.Ok(new NameClipsAfterSections { Project = _rig.Id });
        int steps = _rig.History.UndoCount;

        Assert.True(_rig.Run(new NameClipsAfterSections { Project = _rig.Id }).Succeeded);

        Assert.Equal(steps, _rig.History.UndoCount);
    }

    [Fact]
    public void AProjectWithNoSectionsLeavesEveryNameAlone()
    {
        CommandRig plain = new(_rig.Project with { Sections = [] });
        int steps = plain.History.UndoCount;

        Assert.True(plain.Run(new NameClipsAfterSections { Project = plain.Id }).Succeeded);

        Assert.Equal(steps, plain.History.UndoCount);
        Assert.Equal(["d1", "d2", "d3"], plain.Project.Tracks[0].Clips.Select(clip => clip.Name));
    }

    [Fact]
    public void ALongSectionAndTrackNameTogetherAreCutToTheNameLimitAndStayValid()
    {
        Section longName = _rig.Project.Sections[0] with { Name = new string('s', 150) };
        Track longTrack = _rig.Project.Tracks[0] with { Name = new string('t', 150) };
        ValueList<Section> sections = [longName, _rig.Project.Sections[1]];
        CommandRig rig = new(_rig.Project with { Sections = sections, Tracks = [longTrack, .. _rig.Project.Tracks.Skip(1)] });

        rig.Ok(new NameClipsAfterSections { Project = rig.Id });

        Assert.Equal(ProjectLimits.MaxName, rig.Project.Tracks[0].Clips[0].Name.Length);
    }
}
