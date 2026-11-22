using AudioLab.Core.Commands;
using AudioLab.Core.Model;

namespace AudioLab.Core.Tests;

public sealed class ClipMetaCommandTests
{
    private readonly ClipId _a = ClipId.From("clp_a");

    private readonly ClipId _b = ClipId.From("clp_b");

    private readonly ClipId _c = ClipId.From("clp_c");

    private readonly CommandRig _rig;

    public ClipMetaCommandTests()
    {
        Track audio = BlankProject.Track("trk_1") with { Clips = [Audio("clp_a", 2), Audio("clp_b", 0), Audio("clp_c", 0)] };
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
        _rig = new CommandRig(BlankProject.Create() with { Tracks = [audio, keys with { MidiClips = [midi] }] });
    }

    private static AudioClip Audio(string id, double offset)
    {
        return new AudioClip
        {
            Id = ClipId.From(id),
            Name = id,
            Media = MediaId.From("med_1"),
            Start = 0,
            Duration = 10,
            Offset = offset,
            TrimEnd = 0,
            Gain = 1,
            FadeIn = 0,
            FadeOut = 0,
            Muted = false,
        };
    }

    private AudioClip Clip(ClipId id) => _rig.Project.Tracks[0].Clips.First(clip => clip.Id == id);

    private SetClipScore Score(ClipId clip) => new() { Project = _rig.Id, Clip = clip, Abc = "X:1\nK:C\nCDEF|", Label = "Version A" };

    // ----- score

    [Fact]
    public void AScoreRecordIsSetFieldByFieldAndAFieldLeftOutIsRemovedOnTheNextSet()
    {
        _rig.Ok(Score(_a) with { Truncated = true, BudgetSeconds = 30.5, Seed = -5, Created = 1760000000000, Model = "m", EngineId = "e" });

        ScoreMeta score = Clip(_a).Meta!.Score!;
        Assert.Equal(("X:1\nK:C\nCDEF|", "Version A", true, 30.5, -5L), (score.Abc, score.Label, score.Truncated, score.BudgetSeconds, score.Seed));
        Assert.Equal((1760000000000L, "m", "e"), (score.Created, score.Model, score.EngineId));
        _rig.Ok(Score(_a));
        Assert.Null(Clip(_a).Meta!.Score!.Truncated);
        Assert.Null(Clip(_a).Meta!.Score!.Seed);
    }

    [Fact]
    public void ASetKeepsTheUnknownFieldsOfTheOldScoreAndTheLyricsAndStemOfTheClip()
    {
        using System.Text.Json.JsonDocument future = System.Text.Json.JsonDocument.Parse("""{"x":1}""");
        ScoreMeta old = new() { Label = "Old", Extra = ExtraFields.FromJson(("futureField", future.RootElement.GetRawText())) };
        AudioClip clip = Audio("clp_a", 0) with { Meta = new ClipMeta { Score = old, StemOf = new StemOf(_b, "drums") } };
        CommandRig rig = new(BlankProject.Create() with { Tracks = [BlankProject.Track("trk_1") with { Clips = [clip, Audio("clp_b", 0)] }] });

        rig.Ok(new SetClipScore { Project = rig.Id, Clip = _a, Label = "New" });

        ClipMeta meta = rig.Project.Tracks[0].Clips[0].Meta!;
        Assert.Equal("New", meta.Score!.Label);
        Assert.Equal(old.Extra, meta.Score.Extra);
        Assert.Equal(new StemOf(_b, "drums"), meta.StemOf);
    }

    [Fact]
    public void AScoreWithAnOutOfRangeFieldOrAClipThatIsNotAnAudioClipIsRefusedWithTheRightClass()
    {
        SetClipScore good = Score(_a);

        _rig.RefusedWith(good with { BudgetSeconds = -1 }, CommandErrorCode.Invalid);
        _rig.RefusedWith(good with { BudgetSeconds = double.NaN }, CommandErrorCode.Invalid);
        _rig.RefusedWith(good with { Created = -1 }, CommandErrorCode.Invalid);
        _rig.RefusedWith(good with { Label = new string('l', 201) }, CommandErrorCode.Invalid);
        _rig.RefusedWith(good with { Model = new string('m', 129) }, CommandErrorCode.Invalid);
        _rig.RefusedWith(good with { Abc = new string('a', 1_000_001) }, CommandErrorCode.Invalid);
        _rig.RefusedWith(good with { Abc = "X:1\u0007" }, CommandErrorCode.Invalid);
        _rig.RefusedWith(good with { Clip = ClipId.From("clp_none"), BudgetSeconds = -1 }, CommandErrorCode.NotFound);
        _rig.RefusedWith(good with { Clip = ClipId.From("clp_midi") }, CommandErrorCode.Rejected);
    }

    [Fact]
    public void AParentMustBeAnotherAudioClipOfTheProjectAndMayNotMakeALoop()
    {
        _rig.Ok(Score(_b) with { Parent = _a });
        _rig.Ok(Score(_c) with { Parent = _b });

        _rig.RefusedWith(Score(_a) with { Parent = _a }, CommandErrorCode.Rejected);
        _rig.RefusedWith(Score(_a) with { Parent = _c }, CommandErrorCode.Rejected);
        _rig.RefusedWith(Score(_a) with { Parent = ClipId.From("clp_none"), BudgetSeconds = -1 }, CommandErrorCode.NotFound);
        _rig.RefusedWith(Score(_a) with { Parent = ClipId.From("clp_midi") }, CommandErrorCode.Rejected);
        _rig.Ok(Score(_a) with { Parent = null });
        Assert.Equal(_b, Clip(_c).Meta!.Score!.Parent);
    }

    [Fact]
    public void ALabelIsSetOnAClipThatHasAScoreAndRefusedOnOneThatHasNone()
    {
        _rig.RefusedWith(new SetScoreLabel { Project = _rig.Id, Clip = _a, Label = "x" }, CommandErrorCode.Rejected);
        _rig.Ok(Score(_a));

        _rig.Ok(new SetScoreLabel { Project = _rig.Id, Clip = _a, Label = "Take two" });
        Assert.Equal("Take two", Clip(_a).Meta!.Score!.Label);
        _rig.Ok(new SetScoreLabel { Project = _rig.Id, Clip = _a });

        Assert.Null(Clip(_a).Meta!.Score!.Label);
        Assert.Equal("X:1\nK:C\nCDEF|", Clip(_a).Meta!.Score!.Abc);
        _rig.RefusedWith(new SetScoreLabel { Project = _rig.Id, Clip = _a, Label = new string('l', 201) }, CommandErrorCode.Invalid);
        _rig.RefusedWith(new SetScoreLabel { Project = _rig.Id, Clip = ClipId.From("clp_none"), Label = "x" }, CommandErrorCode.NotFound);
    }

    [Fact]
    public void ClearingAScoreLeavesTheOtherMetadataAndAClipWithNoMetaIsLeftAlone()
    {
        int steps = _rig.History.UndoCount;
        Assert.True(_rig.Run(new ClearClipScore { Project = _rig.Id, Clip = _a }).Succeeded);
        Assert.Equal(steps, _rig.History.UndoCount);
        _rig.Ok(Score(_a));
        _rig.Ok(new SetClipStem { Project = _rig.Id, Clip = _a, Source = _b, Stem = "drums" });

        _rig.Ok(new ClearClipScore { Project = _rig.Id, Clip = _a });

        Assert.Null(Clip(_a).Meta!.Score);
        Assert.NotNull(Clip(_a).Meta!.StemOf);
        _rig.Ok(new ClearClipStem { Project = _rig.Id, Clip = _a });
        Assert.Null(Clip(_a).Meta);
    }

    // ----- stem

    [Fact]
    public void AStemRecordsItsSourceAndAVocalStemTakesTheSourcesLyricsRebasedToStartAtZero()
    {
        _rig.Ok(new ApplyLyricsAlignment
        {
            Project = _rig.Id,
            Clip = _a,
            Text = "la la",
            Words = [new HeardWord("la", 0.5, 1), new HeardWord("la", 1, 2)],
        });
        Assert.Equal([2.5, 3.0], Clip(_a).Meta!.Lyrics!.Words.Select(word => word.Start));

        _rig.Ok(new SetClipStem { Project = _rig.Id, Clip = _b, Source = _a, Stem = "Vocals" });
        _rig.Ok(new SetClipStem { Project = _rig.Id, Clip = _c, Source = _a, Stem = "drums" });

        Assert.Equal(new StemOf(_a, "Vocals"), Clip(_b).Meta!.StemOf);
        Assert.Equal([0.5, 1.0], Clip(_b).Meta!.Lyrics!.Words.Select(word => word.Start));
        Assert.Null(Clip(_c).Meta!.Lyrics);
    }

    [Fact]
    public void AStemOfASourceWithNoLyricsKeepsItsOwn()
    {
        _rig.Ok(new SetLyricsText { Project = _rig.Id, Clip = _b, Text = "own words" });

        _rig.Ok(new SetClipStem { Project = _rig.Id, Clip = _b, Source = _a, Stem = "vocals" });

        Assert.Equal("own words", Clip(_b).Meta!.Lyrics!.Text);
    }

    [Fact]
    public void AStemNeedsAnotherAudioClipAsItsSourceAndANameThatIsNotBlankOrTooLong()
    {
        SetClipStem stem = new() { Project = _rig.Id, Clip = _b, Source = _a, Stem = "bass" };

        _rig.RefusedWith(stem with { Source = _b }, CommandErrorCode.Rejected);
        _rig.RefusedWith(stem with { Stem = "  " }, CommandErrorCode.Rejected);
        _rig.RefusedWith(stem with { Stem = new string('s', 129) }, CommandErrorCode.Invalid);
        _rig.RefusedWith(stem with { Source = ClipId.From("clp_none"), Stem = " " }, CommandErrorCode.NotFound);
        _rig.RefusedWith(stem with { Source = ClipId.From("clp_midi") }, CommandErrorCode.Rejected);
        _rig.RefusedWith(stem with { Clip = ClipId.From("clp_none"), Source = ClipId.From("clp_none2") }, CommandErrorCode.NotFound);
        _rig.RefusedWith(stem with { Clip = ClipId.From("clp_midi") }, CommandErrorCode.Rejected);
        _rig.Ok(stem);
        Assert.Equal(new StemOf(_a, "bass"), Clip(_b).Meta!.StemOf);
    }

    [Fact]
    public void DeletingTheSourceOfAStemClearsItsLinkAsTheClipCommandsAlreadyDo()
    {
        _rig.Ok(new SetClipStem { Project = _rig.Id, Clip = _b, Source = _a, Stem = "bass" });
        _rig.Ok(Score(_c) with { Parent = _a });

        _rig.Ok(new DeleteClip { Project = _rig.Id, Clip = _a });

        Assert.Null(Clip(_b).Meta!.StemOf!.Value.ClipId);
        Assert.Null(Clip(_c).Meta!.Score!.Parent);
    }
}
