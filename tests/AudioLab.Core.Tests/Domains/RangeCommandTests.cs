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

    private readonly TrackId _lead = TrackId.From("trk_lead");

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

    // A clip from 3 to 17 with a note that crosses each edge of the range 6 to 8, one inside it, one after it, and a pedal held through it.
    private static MidiClip Lead()
    {
        return Midi("m3", 3, 14) with
        {
            Notes =
            [
                new MidiNote(new NoteId(1), 60, 0, 1, 100),
                new MidiNote(new NoteId(2), 62, 2, 2, 100),
                new MidiNote(new NoteId(3), 64, 4, 1, 100),
                new MidiNote(new NoteId(4), 65, 7, 1, 100),
            ],
            NextNoteId = new NoteId(5),
            ControlChanges = [new MidiControlChange(1, 64, 127), new MidiControlChange(4, 1, 50)],
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
            Tracks =
            [
                drums,
                bass,
                keys,
                BlankProject.Track("trk_bus", "Bus", TrackKind.Bus),
                ProjectDefaults.NewTrack(_lead, "Lead", TrackKind.Instrument, "#def", synth) with { MidiClips = [Lead()] },
            ],
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
    public void ARangeInAGapRemovesNothingAndInShuffleClosesTheGapForTheLaterClipsOnly()
    {
        ProjectDocument slip = _rig.Project;
        CommandRig shuffle = new(Start(EditMode.Shuffle));
        int steps = _rig.History.UndoCount;

        Assert.Empty(_rig.Created(Delete(13, 16, _drums)));
        Assert.Equal(slip, _rig.Project);
        Assert.Equal(steps, _rig.History.UndoCount);
        Assert.Empty(shuffle.Created(new DeleteRange { Project = shuffle.Id, Start = 13, End = 16, Tracks = [_drums] }));

        Assert.Equal([(2.0, 12.0), (17.0, 21.0)], Spans(shuffle.Project.Tracks[0]));
        Assert.Equal(shuffle.Project.Tracks[1], Start(EditMode.Shuffle).Tracks[1]);
        int shuffleSteps = shuffle.History.UndoCount;
        Assert.Empty(shuffle.Created(new DeleteRange { Project = shuffle.Id, Start = 30, End = 33, Tracks = [_drums] }));
        Assert.Equal(shuffleSteps, shuffle.History.UndoCount);
    }

    [Fact]
    public void ASplitAndADeleteKeepTheSamePiecesAtTheEdgeOfTheMinimum()
    {
        CommandRig split = new(Start(EditMode.Slip));

        split.Created(SplitAt(4, 4.001, _drums));
        _rig.Created(Delete(4, 4.001, _drums));

        Assert.Equal([(2.0, 4.0), (4.0, 4.001), (4.001, 12.0), (20.0, 24.0)], Spans(split.Project.Tracks[0]));
        Assert.Equal([(2.0, 4.0), (4.001, 12.0), (20.0, 24.0)], Spans(Track(_drums)));
    }

    [Fact]
    public void AMidiClipWhollyInsideTheRangeIsRemoved()
    {
        _rig.Created(Delete(3.9, 6.1, _keys));

        Assert.Equal(["m2"], Track(_keys).MidiClips.Select(clip => clip.Id.Value));
    }

    private static string Notes(MidiClip clip) => string.Join(" ", clip.Notes.Select(note => $"{note.Id.Value}:{note.Start}+{note.Duration}"));

    [Fact]
    public void ARangeThatCutsIntoAMidiClipCutsItsNotesAndTheClipKeepsItsPlaceAndLengthInSlip()
    {
        ProjectDocument before = _rig.Project;

        IReadOnlyList<string> created = _rig.Created(Delete(6, 8, _lead));

        MidiClip clip = Track(_lead).MidiClips[0];
        // Note 2 (abs 5 to 7) stops at the range start; note 3 (abs 7 to 8) starts inside the range and ends with it, so it goes without a tail.
        Assert.Equal("1:0+1 2:2+1 4:7+1", Notes(clip));
        Assert.Equal((3.0, 14.0), (clip.Start, clip.Length));
        Assert.Equal(5, clip.NextNoteId.Value);
        Assert.Empty(created);
        Assert.Equal([new MidiControlChange(1, 64, 127), new MidiControlChange(3, 64, 0), new MidiControlChange(5, 64, 127)], clip.ControlChanges.Where(change => change.Controller == 64));
        Assert.DoesNotContain(clip.ControlChanges, change => change.Controller == 1);
        _rig.History.Undo();
        Assert.Equal(before, _rig.Project);
    }

    [Fact]
    public void ANoteThatGoesOnPastTheRangeEndKeepsThatPartAsANewNoteWithTheNextId()
    {
        // The range 4.5 to 6 cuts note 2 (abs 5 to 7) at its start and leaves 6 to 7 as a new note.
        _rig.Created(Delete(4.5, 6, _lead));

        MidiClip clip = Track(_lead).MidiClips[0];
        Assert.Equal("1:0+1 3:4+1 4:7+1 5:3+1", Notes(clip));
        Assert.Equal(6, clip.NextNoteId.Value);
    }

    [Fact]
    public void InShuffleTheClipAcrossTheRangeGetsShorterAndTheLaterNotesAndEventsMoveWithIt()
    {
        CommandRig shuffle = new(Start(EditMode.Shuffle));

        shuffle.Created(new DeleteRange { Project = shuffle.Id, Start = 6, End = 8, Tracks = [_lead] });

        MidiClip clip = shuffle.Project.Tracks[4].MidiClips[0];
        Assert.Equal((3.0, 12.0), (clip.Start, clip.Length));
        Assert.Equal("1:0+1 2:2+1 4:5+1", Notes(clip));
        Assert.Equal([1.0, 3.0, 3.0], clip.ControlChanges.Where(change => change.Controller == 64).Select(change => change.Time));
        Assert.Empty(ProjectValidator.Validate(shuffle.Project));
    }

    [Fact]
    public void ARangeWithNoNotesInItClosesInShuffleForTheLaterClipsAndChangesNothingInSlip()
    {
        ProjectDocument before = _rig.Project;
        CommandRig shuffle = new(Start(EditMode.Shuffle));

        Assert.Empty(_rig.Created(Delete(6.5, 8, _keys)));
        Assert.Empty(shuffle.Created(new DeleteRange { Project = shuffle.Id, Start = 6.5, End = 8, Tracks = [_keys] }));

        Assert.Equal(before, _rig.Project);
        Assert.Equal([(4.0, 2.0), (7.5, 1.0)], shuffle.Project.Tracks[2].MidiClips.Select(clip => (clip.Start, clip.Length)));
    }

    [Fact]
    public void AClipThatWouldUseEveryNoteIdIsRefusedForARangeThatNeedsANewOneAndNothingChanges()
    {
        MidiClip full = Lead() with { NextNoteId = new NoteId(NoteId.Max) };
        CommandRig rig = new(_rig.Project with { Tracks = [.. _rig.Project.Tracks.Take(4), Track(_lead) with { MidiClips = [full] }] });
        ProjectDocument project = rig.Project;

        rig.RefusedWith(new DeleteRange { Project = rig.Id, Start = 4.5, End = 6, Tracks = [_lead] }, CommandErrorCode.Invalid);

        Assert.Equal(project, rig.Project);
        rig.Created(new DeleteRange { Project = rig.Id, Start = 6, End = 8, Tracks = [_lead] });
    }

    [Fact]
    public void AClipThatWouldPassTheNoteLimitIsRefusedAndNothingChanges()
    {
        MidiNote[] spanning = [.. Enumerable.Range(1, ProjectLimits.MaxListItems / 2 + 1).Select(i => new MidiNote(new NoteId(i), 60, 0, 20, 100))];
        MidiClip crowded = Lead() with { Notes = [.. spanning], NextNoteId = new NoteId(spanning.Length + 1), ControlChanges = [] };
        CommandRig rig = new(_rig.Project with { Tracks = [.. _rig.Project.Tracks.Take(4), Track(_lead) with { MidiClips = [crowded] }] });
        ProjectDocument project = rig.Project;

        rig.RefusedWith(new DeleteRange { Project = rig.Id, Start = 6, End = 8, Tracks = [_lead] }, CommandErrorCode.Invalid);

        Assert.Equal(project, rig.Project);
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
            TrackId[] tracks = [_drums, _bass, _keys, _lead];
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
