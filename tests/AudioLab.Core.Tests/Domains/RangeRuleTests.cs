using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Core.Tests;

public sealed class RangeRuleTests
{
    private int _next = 100;

    private ClipId NewId() => ClipId.From($"clp_{_next++}");

    // A clip that plays [start, start + visible) of a 10 second file from its offset.
    private static AudioClip Clip(string id, double start, double visible, double offset = 0, double fadeIn = 0, double fadeOut = 0)
    {
        return new AudioClip
        {
            Id = ClipId.From(id),
            Name = id,
            Media = MediaId.From("med_1"),
            Start = start,
            Duration = 10 + offset,
            Offset = offset,
            TrimEnd = 10 - visible,
            Gain = 1,
            FadeIn = fadeIn,
            FadeOut = fadeOut,
            Muted = false,
            Meta = new ClipMeta { Score = new ScoreMeta { Label = id }, Lyrics = new ClipLyrics { Text = "la", Words = [], Source = "manual" } },
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

    private static (double Start, double End)[] Spans(IEnumerable<AudioClip> clips) => [.. clips.Select(clip => (clip.Start, ClipRules.EndOf(clip)))];

    // ----- delete

    [Fact]
    public void AClipAcrossTheWholeRangeIsCutIntoTwoAndTheRightPieceIsNewWithOnlyTheLyrics()
    {
        AudioClip clip = Clip("a", 2, 10, fadeIn: 0.5, fadeOut: 0.7);

        ValueList<AudioClip> result = RangeRules.Delete([clip], 4, 6, false, NewId);

        Assert.Equal([(2.0, 4.0), (6.0, 12.0)], Spans(result));
        Assert.Equal(("a", "clp_100"), (result[0].Id.Value, result[1].Id.Value));
        Assert.Equal((0.5, 0.0), (result[0].FadeIn, result[0].FadeOut));
        Assert.Equal((0.0, 0.7), (result[1].FadeIn, result[1].FadeOut));
        Assert.Equal(4.0, result[1].Offset);
        Assert.NotNull(result[0].Meta!.Score);
        Assert.Null(result[1].Meta!.Score);
        Assert.NotNull(result[1].Meta!.Lyrics);
    }

    [Fact]
    public void AClipCutOnOneSideKeepsItsIdAndAllItsMetadata()
    {
        AudioClip clip = Clip("a", 2, 10);

        ValueList<AudioClip> before = RangeRules.Delete([clip], 0, 5, false, NewId);
        ValueList<AudioClip> after = RangeRules.Delete([clip], 11, 20, false, NewId);

        Assert.Equal([(5.0, 12.0)], Spans(before));
        Assert.Equal(3.0, before[0].Offset);
        Assert.Equal(("a", clip.Meta), (before[0].Id.Value, before[0].Meta));
        Assert.Equal([(2.0, 11.0)], Spans(after));
        Assert.Equal(("a", clip.Meta), (after[0].Id.Value, after[0].Meta));
        Assert.Equal(100, _next);
    }

    [Fact]
    public void AClipInsideTheRangeIsGoneAndClipsThatOnlyTouchItAreLeftAlone()
    {
        AudioClip inside = Clip("in", 4, 2);
        AudioClip before = Clip("before", 1, 3);
        AudioClip after = Clip("after", 6, 2);

        ValueList<AudioClip> result = RangeRules.Delete([before, inside, after], 4, 6, false, NewId);

        Assert.Equal(["before", "after"], result.Select(clip => clip.Id.Value));
        Assert.Same(before, result[0]);
        Assert.Same(after, result[1]);
    }

    [Fact]
    public void APieceShorterThanAMillisecondIsDroppedAndOneJustOverIsKept()
    {
        AudioClip clip = Clip("a", 2, 10);

        ValueList<AudioClip> thin = RangeRules.Delete([clip], 2.0005, 6, false, NewId);
        ValueList<AudioClip> kept = RangeRules.Delete([clip], 2.0011, 6, false, NewId);

        Assert.Equal([(6.0, 12.0)], Spans(thin));
        Assert.Equal(2, kept.Count);
    }

    [Fact]
    public void ShuffleClosesTheGapForEveryClipFromTheEndOnIncludingTheRightPiece()
    {
        AudioClip across = Clip("across", 2, 10);
        AudioClip later = Clip("later", 20, 3);
        AudioClip start = Clip("start", 6, 1);

        ValueList<AudioClip> result = RangeRules.Delete([across, later, start], 4, 6, true, NewId);

        Assert.Equal([(2.0, 4.0), (4.0, 10.0), (18.0, 21.0), (4.0, 5.0)], Spans(result));
        Assert.Equal(["across", "clp_100", "later", "start"], result.Select(clip => clip.Id.Value));
        Assert.Equal([(2.0, 4.0), (6.0, 12.0), (20.0, 23.0), (6.0, 7.0)], Spans(RangeRules.Delete([across, later, start], 4, 6, false, NewId)));
    }

    [Fact]
    public void AMidiClipWhollyInsideTheRangeIsGoneAndOneThatItCutsIntoIsReportedAndLeftAlone()
    {
        MidiClip inside = Midi("in", 4, 2);
        MidiClip across = Midi("across", 3, 4);
        MidiClip after = Midi("after", 8, 1);
        MidiClip before = Midi("before", 0, 4);

        ValueList<MidiClip> result = RangeRules.Delete([before, inside, across, after], 4, 6, true);

        Assert.Equal(["before", "across", "after"], result.Select(clip => clip.Id.Value));
        Assert.Equal([0.0, 3.0, 6.0], result.Select(clip => clip.Start));
        Assert.True(RangeRules.Cuts(across, 4, 6));
        Assert.False(RangeRules.Cuts(inside, 4, 6));
        Assert.False(RangeRules.Cuts(before, 4, 6));
        Assert.False(RangeRules.Cuts(after, 4, 6));
        Assert.True(RangeRules.Cuts(Midi("edge", 5, 5), 4, 6));
    }

    // ----- split

    [Fact]
    public void ARangeSplitCutsAClipAtBothEdgesAndTheLeftPieceKeepsTheId()
    {
        ValueList<AudioClip> result = RangeRules.Split([Clip("a", 2, 10)], 4, 6, NewId);

        Assert.Equal([(2.0, 4.0), (4.0, 6.0), (6.0, 12.0)], Spans(result));
        Assert.Equal(["a", "clp_100", "clp_101"], result.Select(clip => clip.Id.Value));
    }

    [Fact]
    public void AnEdgeOutsideOrWithinAMillisecondOfAClipEndCutsNothing()
    {
        AudioClip clip = Clip("a", 2, 10);

        Assert.Equal([(2.0, 12.0)], Spans(RangeRules.Split([clip], 0, 1, NewId)));
        Assert.Equal([(2.0, 12.0)], Spans(RangeRules.Split([clip], 2.0005, 11.9995, NewId)));
        Assert.Equal([(2.0, 5.0), (5.0, 12.0)], Spans(RangeRules.Split([clip], 0, 5, NewId)));
    }

    // ----- names

    [Fact]
    public void AClipIsNamedAfterItsSectionThenItsTrackAndALongNameIsCutToTheLimit()
    {
        Section drop = new() { Id = SectionId.From("sec_1"), Name = "Drop 1", Start = 0, End = 8, Color = "#ff0000" };
        Section long1 = drop with { Name = new string('s', 150) };

        Assert.Equal("Drop 1 - supersaw", RangeRules.ClipName(drop, "supersaw"));
        Assert.Equal(ProjectLimits.MaxName, RangeRules.ClipName(long1, new string('t', 150)).Length);
        Assert.StartsWith(new string('s', 150) + " - ", RangeRules.ClipName(long1, new string('t', 150)));
    }
}
