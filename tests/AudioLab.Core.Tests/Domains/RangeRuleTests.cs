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
    public void APieceOfExactlyAMillisecondIsKeptEvenWhenTheSubtractionFallsAHairShort()
    {
        AudioClip clip = Clip("a", 2, 10);

        Assert.Equal(2, RangeRules.Delete([clip], 2.001, 6, false, NewId).Count);
        Assert.Equal(2, RangeRules.Delete([clip], 4, 11.999, false, NewId).Count);
        Assert.Single(RangeRules.Delete([clip], 4, 11.9995, false, NewId));
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
    public void AMidiClipWhollyInsideTheRangeIsGoneTheOthersStayAndShuffleMovesTheLaterOnes()
    {
        MidiClip inside = Midi("in", 4, 2);
        MidiClip across = Midi("across", 3, 4);
        MidiClip after = Midi("after", 8, 1);
        MidiClip before = Midi("before", 0, 4);

        ValueList<MidiClip> shuffled = RangeRules.Delete([before, inside, across, after], 4, 6, true);
        ValueList<MidiClip> slipped = RangeRules.Delete([before, inside, across, after], 4, 6, false);

        Assert.Equal(["before", "across", "after"], shuffled.Select(clip => clip.Id.Value));
        Assert.Equal([0.0, 3.0, 6.0], shuffled.Select(clip => clip.Start));
        Assert.Equal([4.0, 2.0, 1.0], shuffled.Select(clip => clip.Length));
        Assert.Equal([0.0, 3.0, 8.0], slipped.Select(clip => clip.Start));
        Assert.Equal([4.0, 4.0, 1.0], slipped.Select(clip => clip.Length));
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

    [Fact]
    public void ASplitKeepsAPieceOfExactlyAMillisecondOnEitherSideLikeADelete()
    {
        AudioClip clip = Clip("a", 2, 10);

        Assert.Equal(3, RangeRules.Split([clip], 4, 4.001, NewId).Count);
        Assert.Equal(3, RangeRules.Split([clip], 2.001, 11.999, NewId).Count);
        Assert.Equal(2, RangeRules.Split([clip], 4, 4.0005, NewId).Count);
        Assert.Equal(2, RangeRules.Delete([clip], 4, 4.001, false, NewId).Count);
        Assert.Equal(2, RangeRules.Delete([clip], 2.001, 11.999, false, NewId).Count);
    }

    // ----- names

    [Fact]
    public void AClipIsNamedAfterItsSectionThenItsTrackAndALongNameIsCutToTheLimit()
    {
        Section drop = new() { Id = SectionId.From("sec_1"), Name = "Drop 1", Start = 0, End = 8, Color = "#ff0000" };
        Section long1 = drop with { Name = new string('s', 150) };

        Assert.Equal("Drop 1 - supersaw", RangeRules.ClipName(drop, "supersaw"));
        Assert.Equal(new string('s', 47) + " - " + new string('t', 150), RangeRules.ClipName(long1, new string('t', 150)));
    }

    [Fact]
    public void TheSectionPartIsCutFirstSoTheTrackNameStaysWholeAndNoDashDangles()
    {
        Section nearly = new() { Id = SectionId.From("sec_1"), Name = new string('s', 198), Start = 0, End = 8, Color = "#ff0000" };
        Section spaced = nearly with { Name = new string('s', 191) + " " + new string('s', 20) };
        string track = new string('t', 198);

        Assert.Equal(new string('s', 192) + " - Drums", RangeRules.ClipName(nearly, "Drums"));
        Assert.Equal(new string('s', 191) + " - Drums", RangeRules.ClipName(spaced, "Drums"));
        string whole = RangeRules.ClipName(nearly, track);
        Assert.True(whole.Length <= ProjectLimits.MaxName);
        Assert.Equal(new string('s', 198), whole);
        Assert.Equal("s - " + new string('t', 196), RangeRules.ClipName(nearly with { Name = "s" }, track));
    }

    [Fact]
    public void ACutThroughASurrogatePairLeavesWholeTextTheProjectKeeps()
    {
        string emoji = "\U0001F3B8";
        Section drop = new() { Id = SectionId.From("sec_1"), Name = new string('s', 191) + emoji, Start = 0, End = 8, Color = "#ff0000" };

        string name = RangeRules.ClipName(drop, "Drums");

        Assert.True(TextRules.IsClean(name));
        Assert.True(name.Length <= ProjectLimits.MaxName);
        Assert.Equal(new string('s', 191) + " - Drums", name);
    }
}
