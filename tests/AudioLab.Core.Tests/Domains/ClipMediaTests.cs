using AudioLab.Core.Commands;
using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Core.Tests;

public sealed class ClipMediaTests
{
    private readonly MediaId _old = MediaId.From("med_old");

    private readonly MediaId _new = MediaId.From("med_new");

    private AudioClip Clip(double offset = 1, double visible = 4, double duration = 10, ClipMeta? meta = null)
    {
        return new AudioClip
        {
            Id = ClipId.From("clp_1"),
            Name = "Take",
            Media = _old,
            Start = 3,
            Duration = duration,
            Offset = offset,
            TrimEnd = duration - offset - visible,
            Gain = 0.8,
            FadeIn = 0.5,
            FadeOut = 1,
            Muted = true,
            Color = "#123456",
            Meta = meta,
        };
    }

    private static ClipMeta Meta()
    {
        return new ClipMeta
        {
            Score = new ScoreMeta { Label = "Version A" },
            StemOf = new StemOf(ClipId.From("clp_src"), "vocals"),
            Lyrics = new ClipLyrics { Text = "la", Words = [new LyricWord("la", 1.5, 2, 0)], Source = "aligned" },
        };
    }

    // ----- the rule

    [Fact]
    public void TheClipKeepsItsPlaceAndWindowAndTheNewFileLengthIsSet()
    {
        AudioClip replaced = ClipRules.ReplaceMedia(Clip(), _new, 20);

        Assert.Equal((_new, 20.0, 3.0, 1.0), (replaced.Media, replaced.Duration, replaced.Start, replaced.Offset));
        Assert.Equal(4.0, ClipRules.VisibleLength(replaced));
        Assert.Equal((0.8, true, "Take", "#123456"), (replaced.Gain, replaced.Muted, replaced.Name, replaced.Color));
        Assert.Equal((0.5, 1.0), (replaced.FadeIn, replaced.FadeOut));
    }

    [Fact]
    public void AShorterFileCutsTheWindowToItAndFadesToWhatIsLeft()
    {
        AudioClip shorter = ClipRules.ReplaceMedia(Clip(), _new, 3);
        AudioClip tiny = ClipRules.ReplaceMedia(Clip(), _new, 0.04);

        Assert.Equal((1.0, 2.0, 0.0), (shorter.Offset, ClipRules.VisibleLength(shorter), shorter.TrimEnd));
        Assert.Equal((0.5, 1.0), (shorter.FadeIn, shorter.FadeOut));
        Assert.Equal((0.0, 0.04, 0.0), (tiny.Offset, ClipRules.VisibleLength(tiny), tiny.TrimEnd));
        Assert.Equal(0.04, tiny.FadeIn + tiny.FadeOut, 9);
    }

    [Fact]
    public void TheLyricsGoBecauseTheirTimesBelongToTheOldFileAndTheScoreAndStemRecordsStay()
    {
        AudioClip replaced = ClipRules.ReplaceMedia(Clip(meta: Meta()), _new, 20);

        Assert.Null(replaced.Meta!.Lyrics);
        Assert.Equal("Version A", replaced.Meta.Score!.Label);
        Assert.Equal("vocals", replaced.Meta.StemOf!.Value.Stem);
        ClipMeta onlyLyrics = new() { Lyrics = Meta().Lyrics };
        Assert.Null(ClipRules.ReplaceMedia(Clip(meta: onlyLyrics), _new, 20).Meta);
    }

    [Fact]
    public void TheSameFileAndLengthChangeNothingNotEvenTheLyrics()
    {
        AudioClip clip = Clip(meta: Meta());

        Assert.Same(clip, ClipRules.ReplaceMedia(clip, _old, 10));
        Assert.Equal(11.0, ClipRules.ReplaceMedia(clip, _old, 11).Duration);
        Assert.Null(ClipRules.ReplaceMedia(clip, _old, 11).Meta!.Lyrics);
    }

    // ----- the command

    private CommandRig Rig()
    {
        Track audio = BlankProject.Track("trk_1") with { Clips = [Clip(meta: Meta()), Clip() with { Id = ClipId.From("clp_src"), Start = 20 }] };
        Instrument synth = InstrumentPresets.CreateSynth("Soft Keys");
        Track keys = ProjectDefaults.NewTrack(TrackId.From("trk_keys"), "Keys", TrackKind.Instrument, "#abc", synth) with
        {
            MidiClips =
            [
                new MidiClip
                {
                    Id = ClipId.From("clp_midi"),
                    Name = "Midi",
                    Start = 0,
                    Length = 2,
                    Muted = false,
                    Notes = [],
                    NextNoteId = new NoteId(1),
                    ControlChanges = [],
                },
            ],
        };
        return new CommandRig(BlankProject.Create() with { Tracks = [audio, keys] });
    }

    [Fact]
    public void ACommandReplacesTheMediaAndIsOneUndoStepThatBringsTheOldBack()
    {
        CommandRig rig = Rig();
        ProjectDocument before = rig.Project;

        rig.Ok(new ReplaceClipMedia { Project = rig.Id, Clip = ClipId.From("clp_1"), Media = _new, Duration = 20 });

        Assert.Equal((_new, 20.0), (rig.Project.Tracks[0].Clips[0].Media, rig.Project.Tracks[0].Clips[0].Duration));
        Assert.Null(rig.Project.Tracks[0].Clips[0].Meta!.Lyrics);
        rig.History.Undo();
        Assert.Equal(before, rig.Project);
    }

    [Fact]
    public void ReplacingWithTheSameFileAddsNoUndoStep()
    {
        CommandRig rig = Rig();
        int steps = rig.History.UndoCount;

        Assert.True(rig.Run(new ReplaceClipMedia { Project = rig.Id, Clip = ClipId.From("clp_1"), Media = _old, Duration = 10 }).Succeeded);

        Assert.Equal(steps, rig.History.UndoCount);
    }

    [Fact]
    public void AMidiClipOrAMissingClipIsRefusedAndALengthOutsideTheProjectIsInvalid()
    {
        CommandRig rig = Rig();
        ReplaceClipMedia good = new() { Project = rig.Id, Clip = ClipId.From("clp_1"), Media = _new, Duration = 20 };

        rig.RefusedWith(good with { Clip = ClipId.From("clp_midi") }, CommandErrorCode.Rejected);
        rig.RefusedWith(good with { Clip = ClipId.From("clp_none"), Duration = -1 }, CommandErrorCode.NotFound);
        rig.RefusedWith(good with { Duration = 0 }, CommandErrorCode.Invalid);
        rig.RefusedWith(good with { Duration = double.NaN }, CommandErrorCode.Invalid);
        rig.RefusedWith(good with { Duration = ProjectLimits.MaxSeconds + 1 }, CommandErrorCode.Invalid);
        rig.Ok(good with { Duration = ProjectLimits.MaxSeconds });
    }
}
