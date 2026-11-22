using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Core.Tests;

public sealed class ClipRuleTests
{
    // A 20 second file shown from 2 s to 18 s, placed at 10 s: it plays 16 seconds, from 10 to 26.
    private static AudioClip Clip() => Make("clp_a", start: 10, duration: 20, offset: 2, trimEnd: 2);

    internal static AudioClip Make(string id, double start = 0, double duration = 10, double offset = 0, double trimEnd = 0, ClipMeta? meta = null)
    {
        return new AudioClip
        {
            Id = ClipId.From(id),
            Name = id,
            Media = MediaId.From("med_1"),
            Start = start,
            Duration = duration,
            Offset = offset,
            TrimEnd = trimEnd,
            Gain = 1,
            FadeIn = 0,
            FadeOut = 0,
            Muted = false,
            Meta = meta,
        };
    }

    // The time in the file that a point of the timeline plays.
    private static double SourceTime(AudioClip clip, double timeline) => clip.Offset + (timeline - clip.Start);

    [Fact]
    public void AClipPlaysItsWindowOfTheFile()
    {
        AudioClip clip = Clip();

        Assert.Equal(16, ClipRules.VisibleLength(clip));
        Assert.Equal(26, ClipRules.EndOf(clip));
    }

    [Fact]
    public void ASplitMakesTwoClipsThatPlayTheSameAudioWithoutAGapOrAnOverlap()
    {
        AudioClip clip = Clip();

        (AudioClip left, AudioClip right) = ClipRules.Split(clip, 14, ClipId.From("clp_b"));

        Assert.Equal(clip.Id, left.Id);
        Assert.Equal("clp_b", right.Id.Value);
        Assert.Equal((10.0, 14.0), (left.Start, ClipRules.EndOf(left)));
        Assert.Equal((14.0, 26.0), (right.Start, ClipRules.EndOf(right)));
        Assert.Equal(SourceTime(clip, 14), SourceTime(right, 14));
        Assert.Equal(SourceTime(clip, 10), SourceTime(left, 10));
        Assert.Equal((clip.Media, clip.Duration), (right.Media, right.Duration));
    }

    [Fact]
    public void ASplitSharesTheFileAndKeepsGainMuteNameAndColour()
    {
        AudioClip clip = Clip() with { Gain = 0.7, Muted = true, Color = "#abc", Name = "Take" };

        (AudioClip left, AudioClip right) = ClipRules.Split(clip, 14, ClipId.From("clp_b"));

        Assert.All(new[] { left, right }, piece => Assert.Equal((0.7, true, "#abc", "Take", clip.Media), (piece.Gain, piece.Muted, piece.Color, piece.Name, piece.Media)));
    }

    [Fact]
    public void ASplitKeepsTheFadeInOnTheLeftAndTheFadeOutOnTheRightAndNeverMoreThanFits()
    {
        AudioClip clip = Clip() with { FadeIn = 6, FadeOut = 5 };

        (AudioClip left, AudioClip right) = ClipRules.Split(clip, 14, ClipId.From("clp_b"));

        Assert.Equal((4.0, 0.0), (left.FadeIn, left.FadeOut));
        Assert.Equal((0.0, 5.0), (right.FadeIn, right.FadeOut));
        Assert.Equal(1.5, ClipRules.Split(clip with { FadeIn = 1, FadeOut = 20 }, 24.5, ClipId.From("clp_c")).Right.FadeOut);
    }

    [Fact]
    public void ASplitKeepsTheScoreAndStemLinksOnTheLeftAndTheLyricsOnBoth()
    {
        ClipLyrics lyrics = new() { Text = "la", Words = [new LyricWord("la", 0.5, 0.9)] };
        ClipMeta meta = new()
        {
            Score = new ScoreMeta { Abc = "X:1", Label = "v1" },
            Lyrics = lyrics,
            StemOf = new StemOf(ClipId.From("clp_src"), "vocals"),
        };

        (AudioClip left, AudioClip right) = ClipRules.Split(Make("clp_a", 0, 10, meta: meta), 4, ClipId.From("clp_b"));

        Assert.Equal(meta, left.Meta);
        Assert.Equal(lyrics, right.Meta!.Lyrics);
        Assert.Null(right.Meta.Score);
        Assert.Null(right.Meta.StemOf);
    }

    [Theory]
    [InlineData(10, false)]
    [InlineData(10.0005, false)]
    [InlineData(10.002, true)]
    [InlineData(25.998, true)]
    [InlineData(25.9995, false)]
    [InlineData(26, false)]
    [InlineData(5, false)]
    [InlineData(40, false)]
    public void ASplitNeedsAPointInsideTheClipAwayFromBothEnds(double at, bool allowed)
    {
        Assert.Equal(allowed, ClipRules.CanSplit(Clip(), at));
    }

    [Fact]
    public void SplittingAtAnEdgeThrows()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ClipRules.Split(Clip(), 10, ClipId.From("clp_b")));
    }

    [Fact]
    public void TrimmingTheStartKeepsTheAudioAfterItWhereItWas()
    {
        AudioClip clip = Clip();

        AudioClip trimmed = ClipRules.TrimStart(clip, 13);

        Assert.Equal((13.0, 5.0), (trimmed.Start, trimmed.Offset));
        Assert.Equal(SourceTime(clip, 20), SourceTime(trimmed, 20));
        Assert.Equal(ClipRules.EndOf(clip), ClipRules.EndOf(trimmed));
    }

    [Fact]
    public void TheStartCanBeDraggedBackOutOfTheTrimUntilTheFileOrTheProjectStartRunsOut()
    {
        AudioClip clip = Clip();

        AudioClip back = ClipRules.TrimStart(clip, 9);
        AudioClip past = ClipRules.TrimStart(clip, 7);
        AudioClip nearZero = ClipRules.TrimStart(Make("clp_a", start: 1, duration: 10, offset: 4), -50);

        Assert.Equal((9.0, 1.0), (back.Start, back.Offset));
        Assert.Equal((8.0, 0.0), (past.Start, past.Offset));
        Assert.Equal((0.0, 3.0), (nearZero.Start, nearZero.Offset));
    }

    [Fact]
    public void TheStartCannotPassTheMinimumLengthBeforeTheEnd()
    {
        AudioClip trimmed = ClipRules.TrimStart(Clip(), 100);

        Assert.Equal(ClipRules.MinClipLength, ClipRules.VisibleLength(trimmed), 9);
        Assert.Equal(ClipRules.EndOf(Clip()), ClipRules.EndOf(trimmed), 9);
    }

    [Fact]
    public void TrimmingTheEndMovesItAndStopsAtTheFileAndTheMinimumLength()
    {
        AudioClip clip = Clip();

        Assert.Equal(21.0, ClipRules.EndOf(ClipRules.TrimEnd(clip, 21)));
        Assert.Equal(28.0, ClipRules.EndOf(ClipRules.TrimEnd(clip, 99)));
        Assert.Equal(0.0, ClipRules.TrimEnd(clip, 99).TrimEnd);
        Assert.Equal(ClipRules.MinClipLength, ClipRules.VisibleLength(ClipRules.TrimEnd(clip, -5)), 9);
    }

    [Fact]
    public void ATrimShortensFadesThatNoLongerFit()
    {
        AudioClip clip = Clip() with { FadeIn = 8, FadeOut = 8 };

        AudioClip trimmed = ClipRules.TrimEnd(clip, 12);

        Assert.Equal(2.0, ClipRules.VisibleLength(trimmed));
        Assert.Equal((2.0, 2.0), (trimmed.FadeIn, trimmed.FadeOut));
    }

    [Fact]
    public void FadesAreSetLikeADragEachLimitedByWhatTheOtherLeaves()
    {
        AudioClip clip = Make("clp_a", 0, 10) with { FadeIn = 2, FadeOut = 3 };

        AudioClip longIn = ClipRules.SetFades(clip, 50, null);
        AudioClip longOut = ClipRules.SetFades(clip, null, 50);
        AudioClip negative = ClipRules.SetFades(clip, -4, -4);
        AudioClip both = ClipRules.SetFades(clip, 6, 6);

        Assert.Equal((7.0, 3.0), (longIn.FadeIn, longIn.FadeOut));
        Assert.Equal((2.0, 8.0), (longOut.FadeIn, longOut.FadeOut));
        Assert.Equal((0.0, 0.0), (negative.FadeIn, negative.FadeOut));
        Assert.Equal((6.0, 4.0), (both.FadeIn, both.FadeOut));
        Assert.Equal((2.0, 3.0), (ClipRules.SetFades(clip, null, null).FadeIn, ClipRules.SetFades(clip, null, null).FadeOut));
    }

    [Theory]
    [InlineData("Take", "Take (copy)")]
    [InlineData("Take (copy)", "Take (copy 2)")]
    [InlineData("Take (copy 2)", "Take (copy 3)")]
    [InlineData("Take (copy 9)", "Take (copy 10)")]
    [InlineData("A (copy) B", "A (copy) B (copy)")]
    [InlineData("", " (copy)")]
    public void ACopyIsNamedWithoutStackingSuffixes(string name, string expected)
    {
        Assert.Equal(expected, ClipRules.NextCopyName(name));
    }

    [Fact]
    public void ADefaultClipNameIsTheLowestFreeNumberAcrossAudioAndMidiClips()
    {
        ProjectDocument document = BlankProject.Create() with
        {
            Tracks =
            [
                BlankProject.Track("trk_1") with { Clips = [Make("clp_1") with { Name = "Clip 1" }, Make("clp_2") with { Name = "Clip 3" }] },
            ],
        };

        Assert.Equal("Clip 2", ClipRules.NextName(document, "Clip"));
        Assert.Equal("MIDI 1", ClipRules.NextName(document, "MIDI"));
    }

    [Fact]
    public void AMidiClipEndsAtItsStartPlusItsLength()
    {
        MidiClip clip = new()
        {
            Id = ClipId.From("clp_m"),
            Name = "m",
            Start = 4,
            Length = 3,
            NextNoteId = NoteId.First,
            Notes = [],
            ControlChanges = [],
            Muted = false,
        };

        Assert.Equal(7, ClipRules.EndOf(clip));
    }
}
