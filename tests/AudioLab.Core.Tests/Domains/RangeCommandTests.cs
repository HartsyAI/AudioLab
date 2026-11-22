using AudioLab.Core.Commands;
using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Core.Tests;

public sealed class RangeCommandTests
{
    private readonly TrackId _drums = TrackId.From("trk_drums");

    private readonly TrackId _bass = TrackId.From("trk_bass");

    private readonly TrackId _keys = TrackId.From("trk_keys");

    private readonly TrackId _bus = TrackId.From("trk_bus");

    private readonly CommandRig _rig;

    public RangeCommandTests()
    {
        _rig = new CommandRig(Start(EditMode.Slip));
    }

    private static AudioClip Clip(string id, double start, double visible, ClipMeta? meta = null)
    {
        return new AudioClip
        {
            Id = ClipId.From(id),
            Name = id,
            Media = MediaId.From("med_1"),
            Start = start,
            Duration = 10,
            Offset = 0,
            TrimEnd = 10 - visible,
            Gain = 1,
            FadeIn = 0,
            FadeOut = 0,
            Muted = false,
            Meta = meta,
        };
    }

    private static MidiClip Midi(string id, double start, double length)
    {
        return new MidiClip
        {
            Id = ClipId.From(id),
            Name = id,
            Start = start,
            Length = length,
            Muted = false,
            Notes = [],
            NextNoteId = new NoteId(1),
            ControlChanges = [],
        };
    }

    private ProjectDocument Start(EditMode mode)
    {
        Track drums = BlankProject.Track("trk_drums", "Drums") with { Clips = [Clip("d1", 2, 10), Clip("d2", 20, 4)] };
        Track bass = BlankProject.Track("trk_bass", "Bass") with { Clips = [Clip("b1", 1, 5), Clip("b2", 8, 4)] };
        Instrument synth = InstrumentPresets.CreateSynth("Soft Keys");
        Track keys = ProjectDefaults.NewTrack(_keys, "Keys", TrackKind.Instrument, "#abc", synth) with
        {
            MidiClips = [Midi("m1", 4, 2), Midi("m2", 9, 1)],
        };
        ProjectDocument project = BlankProject.Create() with
        {
            Tracks = [drums, bass, keys, BlankProject.Track("trk_bus", "Bus", TrackKind.Bus)],
        };
        return project with { View = project.View with { EditMode = mode } };
    }

    private Track Track(TrackId id) => _rig.Project.Tracks.First(track => track.Id == id);

    private DeleteRange Delete(double start, double end, params TrackId[] tracks)
    {
        return new DeleteRange { Project = _rig.Id, Start = start, End = end, Tracks = [.. tracks] };
    }

    private SplitAtRange SplitAt(double start, double end, params TrackId[] tracks)
    {
        return new SplitAtRange { Project = _rig.Id, Start = start, End = end, Tracks = [.. tracks] };
    }

    private static (double, double)[] Spans(Track track) => [.. track.Clips.Select(clip => (clip.Start, ClipRules.EndOf(clip)))];

    // ----- range_delete

    [Fact]
    public void ADeleteCutsTheClipsOfTheChosenTracksOnlyAndReportsTheNewIds()
    {
        IReadOnlyList<string> created = _rig.Created(Delete(4, 6, _drums));

        Assert.Equal([(2.0, 4.0), (6.0, 12.0), (20.0, 24.0)], Spans(Track(_drums)));
        Assert.Equal([created[0]], [Track(_drums).Clips[1].Id.Value]);
        Assert.Single(created);
        Assert.Equal([(1.0, 6.0), (8.0, 12.0)], Spans(Track(_bass)));
    }

    [Fact]
    public void InShuffleEditModeTheLaterClipsCloseTheGapAndOtherwiseTheyStay()
    {
        CommandRig shuffle = new(Start(EditMode.Shuffle));

        shuffle.Created(new DeleteRange { Project = shuffle.Id, Start = 4, End = 6, Tracks = [_drums, _keys] });

        Assert.Equal([(2.0, 4.0), (4.0, 10.0), (18.0, 22.0)], Spans(shuffle.Project.Tracks[0]));
        Assert.Equal(["m2"], shuffle.Project.Tracks[2].MidiClips.Select(clip => clip.Id.Value));
        Assert.Equal(7.0, shuffle.Project.Tracks[2].MidiClips[0].Start);
        Assert.Equal([(1.0, 6.0), (8.0, 12.0)], Spans(shuffle.Project.Tracks[1]));
    }

    [Fact]
    public void AMidiClipWhollyInsideTheRangeIsRemovedAndOneItCutsIntoIsRejectedAndNothingChanges()
    {
        CommandResult refused = _rig.Refused(Delete(5, 8, _keys, _drums));

        Assert.Equal(CommandErrorCode.Rejected, refused.Errors[0].Code);
        _rig.Created(Delete(3.9, 6.1, _keys));
        Assert.Equal(["m2"], Track(_keys).MidiClips.Select(clip => clip.Id.Value));
    }

    [Fact]
    public void ADeleteClearsTheLinksOtherClipsHadToAClipItRemoved()
    {
        ScoreMeta child = new() { Parent = ClipId.From("d2"), Label = "child" };
        ProjectDocument project = Start(EditMode.Slip);
        Track drums = project.Tracks[0];
        AudioClip linked = Clip("d3", 30, 2, new ClipMeta { Score = child });
        CommandRig linkedRig = new(project with { Tracks = [drums with { Clips = [.. drums.Clips, linked] }, .. project.Tracks.Skip(1)] });

        linkedRig.Created(new DeleteRange { Project = linkedRig.Id, Start = 19, End = 25, Tracks = [_drums] });

        Assert.Null(linkedRig.Project.Tracks[0].Clips.First(clip => clip.Id.Value == "d3").Meta!.Score!.Parent);
    }

    [Fact]
    public void ADeleteThatChangesNothingSucceedsWithoutAnUndoStepOrAnId()
    {
        int steps = _rig.History.UndoCount;

        CommandResult result = _rig.Run(Delete(14, 18, _drums, _bass, _bus));

        Assert.True(result.Succeeded);
        Assert.Empty(result.Created);
        Assert.Equal(steps, _rig.History.UndoCount);
    }

    [Fact]
    public void ADeleteIsOneUndoStepThatBringsTheClipsBack()
    {
        ProjectDocument before = _rig.Project;

        _rig.Created(Delete(4, 6, _drums, _bass));
        _rig.History.Undo();

        Assert.Equal(before, _rig.Project);
    }

    [Fact]
    public void TheTracksAreLookedUpBeforeTheTimesAndBadTimesAreInvalid()
    {
        _rig.RefusedWith(Delete(double.NaN, 6, TrackId.From("trk_none")), CommandErrorCode.NotFound);
        _rig.RefusedWith(Delete(4, 6), CommandErrorCode.Invalid);
        _rig.RefusedWith(Delete(-1, 6, _drums), CommandErrorCode.Invalid);
        _rig.RefusedWith(Delete(4, ProjectLimits.MaxSeconds + 1, _drums), CommandErrorCode.Invalid);
        _rig.RefusedWith(Delete(double.NaN, 6, _drums), CommandErrorCode.Invalid);
        _rig.RefusedWith(Delete(4, 4, _drums), CommandErrorCode.Invalid);
        _rig.RefusedWith(Delete(6, 4, _drums), CommandErrorCode.Invalid);
        _rig.RefusedWith(Delete(4, 4.0005, _drums), CommandErrorCode.Invalid);
        _rig.Ok(Delete(4, 4.001, _drums));
    }

    [Fact]
    public void ATrackNamedTwiceIsCutOnceAndAFullTrackIsRefused()
    {
        _rig.Created(Delete(4, 6, _drums, _drums));
        Assert.Equal(3, Track(_drums).Clips.Count);

        Track full = BlankProject.Track("trk_full") with
        {
            Clips = [.. Enumerable.Range(0, ProjectLimits.MaxClips).Select(i => Clip($"f{i}", 0, 10))],
        };
        CommandRig rig = new(BlankProject.Create() with { Tracks = [full] });

        CommandResult refused = rig.Refused(new DeleteRange { Project = rig.Id, Start = 4, End = 6, Tracks = [TrackId.From("trk_full")] });

        Assert.Equal(CommandErrorCode.Invalid, refused.Errors[0].Code);
        Assert.Contains("at most", refused.Errors[0].Message);
    }

    [Fact]
    public void ARefusedDeleteUsesNoIdSoTheNextOneIsTheNextInLine()
    {
        Track full = BlankProject.Track("trk_full") with
        {
            Clips = [.. Enumerable.Range(0, ProjectLimits.MaxClips).Select(i => Clip($"f{i}", 0, 10))],
        };
        Track small = BlankProject.Track("trk_small") with { Clips = [Clip("s1", 0, 10)] };
        CommandRig rig = new(BlankProject.Create() with { Tracks = [small, full] });
        TrackId fullId = TrackId.From("trk_full");
        TrackId smallId = TrackId.From("trk_small");

        rig.RefusedWith(new DeleteRange { Project = rig.Id, Start = 4, End = 6, Tracks = [smallId, fullId] }, CommandErrorCode.Invalid);
        rig.RefusedWith(new SplitAtRange { Project = rig.Id, Start = 4, End = 6, Tracks = [smallId, fullId] }, CommandErrorCode.Invalid);
        IReadOnlyList<string> created = rig.Created(new DeleteRange { Project = rig.Id, Start = 4, End = 6, Tracks = [smallId] });

        Assert.Equal(["clp_1001"], created);
    }

    [Theory]
    [InlineData(EditMode.Slip)]
    [InlineData(EditMode.Shuffle)]
    public void AHundredRandomRangesLeaveAValidProjectOrAreRefusedWithAnExplanation(EditMode mode)
    {
        Random random = new(5);
        CommandRig rig = new(Start(mode));

        foreach (int i in Enumerable.Range(0, 100))
        {
            double start = Math.Round(random.NextDouble() * 30, 3);
            double end = Math.Round(start + (random.NextDouble() * 6), 3);
            TrackId[] tracks = [_drums, _bass, _keys];
            Command command = i % 2 == 0
                ? new DeleteRange { Project = rig.Id, Start = start, End = end, Tracks = [.. tracks] }
                : new SplitAtRange { Project = rig.Id, Start = start, End = end, Tracks = [.. tracks] };

            CommandResult result = rig.Run(command);

            string why = string.Join("; ", result.Errors.Select(error => error.Message));
            Assert.True(result.Succeeded || result.Errors[0].Code is CommandErrorCode.Invalid or CommandErrorCode.Rejected, why);
            Assert.Empty(ProjectValidator.Validate(rig.Project));
        }
    }

    // ----- range_split

    [Fact]
    public void ASplitCutsEveryClipAcrossAnEdgeAndReportsTheNewIdsInOrder()
    {
        IReadOnlyList<string> created = _rig.Created(SplitAt(4, 11, _drums, _bass));

        Assert.Equal([(2.0, 4.0), (4.0, 11.0), (11.0, 12.0), (20.0, 24.0)], Spans(Track(_drums)));
        Assert.Equal([(1.0, 4.0), (4.0, 6.0), (8.0, 11.0), (11.0, 12.0)], Spans(Track(_bass)));
        Assert.Equal(["clp_1001", "clp_1002", "clp_1003", "clp_1004"], created);
        Assert.Equal(["d1", "clp_1001", "clp_1002", "d2"], Track(_drums).Clips.Select(clip => clip.Id.Value));
        Assert.Equal(["b1", "clp_1003", "b2", "clp_1004"], Track(_bass).Clips.Select(clip => clip.Id.Value));
    }

    [Fact]
    public void ASplitOfNothingToCutSucceedsAndAddsNoUndoStepOrIdAndLeavesMidiClipsAlone()
    {
        int steps = _rig.History.UndoCount;

        CommandResult result = _rig.Run(SplitAt(13, 18, _drums, _keys, _bus));

        Assert.True(result.Succeeded);
        Assert.Empty(result.Created);
        Assert.Equal(steps, _rig.History.UndoCount);
        Assert.Equal(2, Track(_keys).MidiClips.Count);
        _rig.Created(SplitAt(5, 5.5, _keys));
        Assert.Equal(2, Track(_keys).MidiClips.Count);
    }

    [Fact]
    public void ASplitRefusesBadTimesAndTracksLikeADeleteDoes()
    {
        _rig.RefusedWith(SplitAt(1, 2, TrackId.From("trk_none")), CommandErrorCode.NotFound);
        _rig.RefusedWith(SplitAt(6, 4, _drums), CommandErrorCode.Invalid);
        _rig.RefusedWith(SplitAt(double.NaN, 4, _drums), CommandErrorCode.Invalid);
        _rig.RefusedWith(SplitAt(1, 2), CommandErrorCode.Invalid);
    }
}
