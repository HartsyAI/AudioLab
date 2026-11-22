using AudioLab.Core.Commands;
using AudioLab.Core.Model;

namespace AudioLab.Core.Tests;

public sealed class LyricsCommandTests
{
    private static readonly ClientId _client = ClientId.From("test-client");

    private readonly ClipId _clip = ClipId.From("clp_1");

    private readonly ClipId _vocals = ClipId.From("clp_vocals");

    private readonly ClipId _drums = ClipId.From("clp_drums");

    private readonly CommandRig _rig;

    public LyricsCommandTests()
    {
        // A clip heard from 1 s to 9 s of its file (offset 1, trimmed 1 s off the end of 10), with a vocal stem and a drum stem of it.
        AudioClip source = Audio("clp_1", null) with { Start = 3, Offset = 1, TrimEnd = 1, Duration = 10 };
        AudioClip vocals = Audio("clp_vocals", new StemOf(_clip, "Vocals"));
        AudioClip drums = Audio("clp_drums", new StemOf(_clip, "drums"));
        Instrument synth = InstrumentPresets.CreateSynth("Soft Keys");
        Track keys = ProjectDefaults.NewTrack(TrackId.From("trk_keys"), "Keys", TrackKind.Instrument, "#abc", synth);
        MidiClip midi = new()
        {
            Id = ClipId.From("clp_midi"),
            Name = "Midi",
            Start = 0,
            Length = 4,
            Muted = false,
            Notes = [],
            NextNoteId = new NoteId(1),
            ControlChanges = [],
        };
        _rig = new CommandRig(BlankProject.Create() with
        {
            Tracks = [BlankProject.Track("trk_1") with { Clips = [source, vocals, drums] }, keys with { MidiClips = [midi] }],
        });
    }

    private static AudioClip Audio(string id, StemOf? stem)
    {
        return new AudioClip
        {
            Id = ClipId.From(id),
            Name = id,
            Media = MediaId.From("med_1"),
            Start = 0,
            Duration = 10,
            Offset = 0,
            TrimEnd = 0,
            Gain = 1,
            FadeIn = 0,
            FadeOut = 0,
            Muted = false,
            Meta = stem is null ? null : new ClipMeta { StemOf = stem },
        };
    }

    private AudioClip Clip(ClipId id) => _rig.Project.Tracks[0].Clips.First(clip => clip.Id == id);

    private ClipLyrics? Lyrics(ClipId id) => Clip(id).Meta?.Lyrics;

    private static ValueList<HeardWord> Heard(params (string Word, double Start, double End)[] words)
    {
        return [.. words.Select(word => new HeardWord(word.Word, word.Start, word.End))];
    }

    private void Type(string text) => _rig.Ok(new SetLyricsText { Project = _rig.Id, Clip = _clip, Text = text });

    private void Align(string text, params (string Word, double Start, double End)[] words)
    {
        _rig.Ok(new ApplyLyricsAlignment { Project = _rig.Id, Clip = _clip, Text = text, Words = Heard(words) });
    }

    // ----- text

    [Fact]
    public void TypedTextMakesManualLyricsWithNoWordsAndAnEmptyTextRemovesThemAndTheMetaWithThem()
    {
        Type("[Verse]\nhello world");

        Assert.Equal(("[Verse]\nhello world", "manual", 0), (Lyrics(_clip)!.Text, Lyrics(_clip)!.Source, Lyrics(_clip)!.Words.Count));
        Type("  \n[Chorus]\n");
        Assert.Null(Clip(_clip).Meta);
    }

    [Fact]
    public void RemovingTheLyricsKeepsTheScoreOfTheClipAndTheScoreKeepsItsLyricsWhenTheTextIsCleared()
    {
        _rig.Ok(new SetClipScore { Project = _rig.Id, Clip = _clip, Label = "A" });
        Type("la la");

        Type(string.Empty);

        Assert.Null(Lyrics(_clip));
        Assert.Equal("A", Clip(_clip).Meta!.Score!.Label);
    }

    [Fact]
    public void ANewTextKeepsTheTimesWhenOnlyTheSpellingAroundTheWordsChanged()
    {
        Align("hello world", ("hello", 0, 1), ("world", 1, 2));

        Type("Hello,\nworld!");

        Assert.Equal(["Hello,", "world!"], Lyrics(_clip)!.Words.Select(word => word.Word));
        Assert.Equal([2.0, 3.0], Lyrics(_clip)!.Words.Select(word => word.End));
        Type("hello there");
        Assert.Empty(Lyrics(_clip)!.Words);
    }

    [Fact]
    public void TheTextOfAClipThatIsNotThereOrIsMidiOrIsNotCleanOrIsTooLongIsRefusedWithTheRightClass()
    {
        SetLyricsText good = new() { Project = _rig.Id, Clip = _clip, Text = "la" };

        _rig.RefusedWith(good with { Clip = ClipId.From("clp_none") }, CommandErrorCode.NotFound);
        _rig.RefusedWith(good with { Clip = ClipId.From("clp_midi") }, CommandErrorCode.Rejected);
        _rig.RefusedWith(good with { Text = "la\u0007la" }, CommandErrorCode.Invalid);
        _rig.RefusedWith(good with { Text = new string('a', ProjectLimits.MaxLongText + 1) }, CommandErrorCode.Invalid);
        _rig.Ok(good with { Text = new string('a', ProjectLimits.MaxLongText) });
    }

    // ----- the vocal stem follows

    [Fact]
    public void TheVocalStemTakesTheLyricsRebasedToStartWhereTheClipDoesAndTheDrumStemTakesNone()
    {
        Align("la la", ("la", 0.25, 0.75), ("la", 1, 2));

        // The aligner heard audio that began at the clip's offset (1 s), so the words sit at 1.25 and 2 in the source.
        Assert.Equal([1.25, 2.0], Lyrics(_clip)!.Words.Select(word => word.Start));
        Assert.Equal([0.25, 1.0], Lyrics(_vocals)!.Words.Select(word => word.Start));
        Assert.Equal("la la", Lyrics(_vocals)!.Text);
        Assert.Null(Lyrics(_drums));
        Assert.Equal("drums", Clip(_drums).Meta!.StemOf!.Value.Stem);
    }

    [Fact]
    public void ClearingTheLyricsOfTheSourceClearsTheStemsAndEveryEditFollows()
    {
        Align("la la", ("la", 0.25, 0.75), ("la", 1, 2));

        _rig.Ok(new ShiftLyrics { Project = _rig.Id, Clip = _clip, Seconds = 0.5 });
        Assert.Equal([0.75, 1.5], Lyrics(_vocals)!.Words.Select(word => word.Start));
        Type(string.Empty);

        Assert.Null(Lyrics(_vocals));
        Assert.Equal(new StemOf(_clip, "Vocals"), Clip(_vocals).Meta!.StemOf);
    }

    // ----- model answers

    [Fact]
    public void AlignedWordsStartWhereTheClipsOffsetIsUnlessTheCommandSaysOtherwise()
    {
        Align("hello", ("hello", 0.5, 1));
        Assert.Equal((1.5, 2.0, "aligned"), (Lyrics(_clip)!.Words[0].Start, Lyrics(_clip)!.Words[0].End, Lyrics(_clip)!.Source));

        _rig.Ok(new ApplyLyricsAlignment
        {
            Project = _rig.Id,
            Clip = _clip,
            Text = "hello",
            Words = Heard(("hello", 0.5, 1)),
            Offset = 4,
            Model = "aligner-1",
            Source = "forced",
        });

        Assert.Equal((4.5, "aligner-1", "forced"), (Lyrics(_clip)!.Words[0].Start, Lyrics(_clip)!.Model, Lyrics(_clip)!.Source));
    }

    [Fact]
    public void AnAnswerWithNoUsableWordOrNoTextToTimeIsRefusedAsRejectedAndOneOutOfRangeAsInvalid()
    {
        ApplyLyricsAlignment align = new() { Project = _rig.Id, Clip = _clip, Text = "hello", Words = Heard(("hello", 0, 1)) };

        _rig.RefusedWith(align with { Words = Heard((" ", 0, 1)) }, CommandErrorCode.Rejected);
        _rig.RefusedWith(align with { Text = "[Chorus]" }, CommandErrorCode.Rejected);
        _rig.RefusedWith(align with { Words = [] }, CommandErrorCode.Invalid);
        _rig.RefusedWith(align with { Words = Heard(("a", double.NaN, 1)) }, CommandErrorCode.Invalid);
        _rig.RefusedWith(align with { Words = Heard(("a", -1, 1)) }, CommandErrorCode.Invalid);
        _rig.RefusedWith(align with { Words = Heard((new string('w', 201), 0, 1)) }, CommandErrorCode.Invalid);
        _rig.RefusedWith(align with { Offset = -1 }, CommandErrorCode.Invalid);
        _rig.RefusedWith(align with { Model = new string('m', 129) }, CommandErrorCode.Invalid);
        _rig.RefusedWith(align with { Source = new string('s', 129) }, CommandErrorCode.Invalid);
        _rig.RefusedWith(align with { Clip = ClipId.From("clp_none"), Words = [] }, CommandErrorCode.NotFound);
        _rig.RefusedWith(align with { Clip = ClipId.From("clp_midi") }, CommandErrorCode.Rejected);
    }

    [Fact]
    public void AWordTheValidatorCannotHoldRefusesTheWholeAnswerAndChangesNothing()
    {
        _rig.RefusedWith(
            new ApplyLyricsAlignment { Project = _rig.Id, Clip = _clip, Text = "a", Words = Heard(("a\u0007", 0, 1)) },
            CommandErrorCode.Invalid);
        _rig.RefusedWith(
            new ApplyLyricsAlignment
            {
                Project = _rig.Id,
                Clip = _clip,
                Text = "a",
                Words = Heard(("a", ProjectLimits.MaxSeconds, ProjectLimits.MaxSeconds)),
                Offset = 5,
            },
            CommandErrorCode.Invalid);
    }

    [Fact]
    public void ATranscriptBecomesLyricsBrokenIntoLinesByPausesAndFlagsNothingInventedByTheTimes()
    {
        _rig.Ok(new ApplyLyricsTranscript
        {
            Project = _rig.Id,
            Clip = _clip,
            Words = Heard(("hello", 0, 0.5), ("there", 0.5, 1), ("friend", 3, 3.5)),
            Model = "stt-1",
        });

        Assert.Equal(("hello there\nfriend", "stt", "stt-1"), (Lyrics(_clip)!.Text, Lyrics(_clip)!.Source, Lyrics(_clip)!.Model));
        Assert.Equal([0, 0, 1], Lyrics(_clip)!.Words.Select(word => word.Line!.Value));
        Assert.Equal(1.0, Lyrics(_clip)!.Words[0].Start);
        _rig.RefusedWith(new ApplyLyricsTranscript { Project = _rig.Id, Clip = _clip, Words = [] }, CommandErrorCode.Invalid);
        _rig.RefusedWith(new ApplyLyricsTranscript { Project = _rig.Id, Clip = _clip, Words = Heard((" ", 0, 1)) }, CommandErrorCode.Rejected);
    }

    // ----- estimate, clear, shift

    [Fact]
    public void EstimatingSpreadsTheTextOverTheVisiblePartOfTheClipInSourceSeconds()
    {
        Type("one two\nthree");

        _rig.Ok(new EstimateLyricsTiming { Project = _rig.Id, Clip = _clip });

        // The clip shows 1 s to 9 s of its file: 8 s shared by two lines.
        Assert.Equal(("estimated", 1.0), (Lyrics(_clip)!.Source, Lyrics(_clip)!.Words[0].Start));
        Assert.Equal(5.0, Lyrics(_clip)!.Words[^1].Start);
        Assert.True(Lyrics(_clip)!.Words[^1].End <= 9.0 + 1e-9);
    }

    [Fact]
    public void EstimatingTakesTheGivenTextInPlaceOfTheClipsAndRefusesWhenThereIsNone()
    {
        _rig.RefusedWith(new EstimateLyricsTiming { Project = _rig.Id, Clip = _clip }, CommandErrorCode.Rejected);
        _rig.RefusedWith(new EstimateLyricsTiming { Project = _rig.Id, Clip = _clip, Text = "[Verse]" }, CommandErrorCode.Rejected);
        _rig.RefusedWith(new EstimateLyricsTiming { Project = _rig.Id, Clip = _clip, Text = new string('a', 1_000_001) }, CommandErrorCode.Invalid);

        _rig.Ok(new EstimateLyricsTiming { Project = _rig.Id, Clip = _clip, Text = "la la" });

        Assert.Equal("la la", Lyrics(_clip)!.Text);
    }

    [Fact]
    public void ClearingTheTimingKeepsTheTextAndAClipWithNoLyricsIsLeftAlone()
    {
        int steps = _rig.History.UndoCount;
        Assert.True(_rig.Run(new ClearLyricsTiming { Project = _rig.Id, Clip = _clip }).Succeeded);
        Assert.Equal(steps, _rig.History.UndoCount);
        Align("la la", ("la", 0, 1), ("la", 1, 2));

        _rig.Ok(new ClearLyricsTiming { Project = _rig.Id, Clip = _clip });

        Assert.Equal(("la la", "manual", 0), (Lyrics(_clip)!.Text, Lyrics(_clip)!.Source, Lyrics(_clip)!.Words.Count));
        Assert.Empty(Lyrics(_vocals)!.Words);
    }

    [Fact]
    public void ShiftingMovesEveryWordNeverBeforeZeroAndIsLeftAloneWithoutTimedWords()
    {
        int steps = _rig.History.UndoCount;
        Assert.True(_rig.Run(new ShiftLyrics { Project = _rig.Id, Clip = _clip, Seconds = 1 }).Succeeded);
        Type("la");
        Assert.True(_rig.Run(new ShiftLyrics { Project = _rig.Id, Clip = _clip, Seconds = 1 }).Succeeded);
        Assert.Equal(steps + 1, _rig.History.UndoCount);
        Align("la la", ("la", 0, 1), ("la", 2, 3));

        _rig.Ok(new ShiftLyrics { Project = _rig.Id, Clip = _clip, Seconds = -50 });

        Assert.Equal([0.0, 2.0], Lyrics(_clip)!.Words.Select(word => word.Start));
        _rig.RefusedWith(new ShiftLyrics { Project = _rig.Id, Clip = _clip, Seconds = double.NaN }, CommandErrorCode.Invalid);
        _rig.RefusedWith(new ShiftLyrics { Project = _rig.Id, Clip = _clip, Seconds = ProjectLimits.MaxSeconds + 1 }, CommandErrorCode.Invalid);
        _rig.RefusedWith(new ShiftLyrics { Project = _rig.Id, Clip = _clip, Seconds = ProjectLimits.MaxSeconds }, CommandErrorCode.Invalid);
    }

    // ----- one word

    [Fact]
    public void AWordTimeIsSetInsideItsNeighboursAndMissingWordsAreNotFoundBeforeAnyValueIsLooked()
    {
        Align("a b c", ("a", 0, 1), ("b", 1, 2), ("c", 2, 3));
        SetLyricsWordTime set = new() { Project = _rig.Id, Clip = _clip, Word = 1 };

        _rig.Ok(set with { End = 9 });
        _rig.Ok(set with { Start = 0.5 });

        Assert.Equal((1.0, 4.0), (Lyrics(_clip)!.Words[1].Start, Lyrics(_clip)!.Words[1].End));
        _rig.RefusedWith(set with { Word = 3, Start = double.NaN }, CommandErrorCode.NotFound);
        _rig.RefusedWith(set with { Word = -1 }, CommandErrorCode.NotFound);
        _rig.RefusedWith(set, CommandErrorCode.Invalid);
        _rig.RefusedWith(set with { Start = -1 }, CommandErrorCode.Invalid);
        _rig.RefusedWith(set with { End = double.PositiveInfinity }, CommandErrorCode.Invalid);
        Type(string.Empty);
        _rig.RefusedWith(set with { Start = 1 }, CommandErrorCode.NotFound);
    }

    [Fact]
    public void DraggingAWordEdgeIsOneGestureAndEveryFrameSucceeds()
    {
        Align("a b", ("a", 0, 1), ("b", 1, 2));
        GestureId drag = GestureId.From("word-drag");
        int steps = _rig.History.UndoCount;

        foreach (double start in new[] { 2.2, 2.4, 2.6, 2.8 })
        {
            CommandResult result = _rig.Run(new SetLyricsWordTime { Project = _rig.Id, Clip = _clip, Word = 1, Start = start }, drag);
            Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(error => error.Message)));
        }
        _rig.History.EndGesture(_client, drag);

        Assert.Equal(steps + 1, _rig.History.UndoCount);
        Assert.Equal(2.8, Lyrics(_clip)!.Words[1].Start);
        _rig.History.Undo();
        Assert.Equal(2.0, Lyrics(_clip)!.Words[1].Start);
    }

    [Fact]
    public void ANudgeAndAWordEditOfAStemThemselvesAreNotCopiedBackToTheSource()
    {
        Align("la", ("la", 0, 1));

        _rig.Ok(new SetLyricsWordTime { Project = _rig.Id, Clip = _vocals, Word = 0, Start = 0.5 });

        Assert.Equal(0.5, Lyrics(_vocals)!.Words[0].Start);
        Assert.Equal(1.0, Lyrics(_clip)!.Words[0].Start);
    }

    // ----- LRC

    [Fact]
    public void AnLrcFileIsTimedFromTheTimelineIntoTheClipsSourceSeconds()
    {
        string lrc = "[00:04.00]one two\n[00:06.00]three";

        _rig.Ok(new ImportLyricsLrc { Project = _rig.Id, Clip = _clip, Lrc = lrc, LastLine = 2 });

        // The clip starts at 3 s on the timeline and its audio at 1 s of the file, so timeline 4 s is source 2 s.
        Assert.Equal(("lrc", "one two\nthree", 2.0), (Lyrics(_clip)!.Source, Lyrics(_clip)!.Text, Lyrics(_clip)!.Words[0].Start));
        Assert.Equal(4.0, Lyrics(_clip)!.Words[2].Start);
        Assert.Equal(1.0, Lyrics(_vocals)!.Words[0].Start);
    }

    [Fact]
    public void AnLrcFileWithNoTimedLinesOrABadLastLineIsRefused()
    {
        ImportLyricsLrc import = new() { Project = _rig.Id, Clip = _clip, Lrc = "[00:01.00]la" };

        _rig.RefusedWith(import with { Lrc = "no stamps here" }, CommandErrorCode.Rejected);
        _rig.RefusedWith(import with { LastLine = -1 }, CommandErrorCode.Invalid);
        _rig.RefusedWith(import with { LastLine = double.NaN }, CommandErrorCode.Invalid);
        _rig.RefusedWith(import with { Lrc = new string('x', 1_000_001) }, CommandErrorCode.Invalid);
        _rig.Ok(import);
    }

    [Fact]
    public void EveryLyricsCommandIsRefusedOnAMidiClipAndOnAClipThatIsNotThere()
    {
        ProjectId id = _rig.Id;
        foreach (ClipId target in new[] { ClipId.From("clp_midi"), ClipId.From("clp_none") })
        {
            CommandErrorCode expected = target.Value == "clp_midi" ? CommandErrorCode.Rejected : CommandErrorCode.NotFound;
            Command[] commands =
            [
                new SetLyricsText { Project = id, Clip = target, Text = "la" },
                new ApplyLyricsAlignment { Project = id, Clip = target, Text = "la", Words = Heard(("la", 0, 1)) },
                new ApplyLyricsTranscript { Project = id, Clip = target, Words = Heard(("la", 0, 1)) },
                new EstimateLyricsTiming { Project = id, Clip = target, Text = "la" },
                new ClearLyricsTiming { Project = id, Clip = target },
                new ShiftLyrics { Project = id, Clip = target, Seconds = 1 },
                new SetLyricsWordTime { Project = id, Clip = target, Word = 0, Start = 1 },
                new ImportLyricsLrc { Project = id, Clip = target, Lrc = "[00:01.00]la" },
            ];

            Assert.All(commands, command => _rig.RefusedWith(command, expected));
        }
    }
}
