using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Core.Tests;

public sealed class LyricsRuleTests
{
    private static ValueList<LyricWord> Words(params (string Word, double Start, double End)[] words)
    {
        return [.. words.Select(word => new LyricWord(word.Word, word.Start, word.End, 0))];
    }

    private static ClipLyrics Lyrics(string text, ValueList<LyricWord> words, string source = "manual")
    {
        return new ClipLyrics { Text = text, Words = words, Source = source };
    }

    private sealed class TolerantComparer : IEqualityComparer<double>
    {
        public bool Equals(double x, double y) => Math.Abs(x - y) < 1e-9;

        public int GetHashCode(double value) => 0;
    }

    private static ClipLyrics? Lrc(string file, Func<double, double> toSource, double lastLine = 4)
    {
        return LyricsRules.FromLrc(LyricsRules.ParseLrc(file, out _), toSource, lastLine);
    }

    private static int[] Flagged(ValueList<LyricWord> words)
    {
        return [.. words.Select((word, i) => (word, i)).Where(pair => pair.word.Unsure == true).Select(pair => pair.i)];
    }

    // ----- text

    [Fact]
    public void TheLinesOfATextDropTagsAndEmptyLinesAndAreTrimmed()
    {
        Assert.Equal(["hello world", "la la"], LyricsRules.LinesOf("[Verse 1]\n  hello world  \n\n[Chorus]\r\nla la\r\n"));
        Assert.Empty(LyricsRules.LinesOf("[Intro]\n   \n"));
        Assert.Empty(LyricsRules.LinesOf(null));
        Assert.Equal(["Hello,", "world", "la"], LyricsRules.WordsOf("[V]\nHello, world\nla"));
    }

    [Theory]
    [InlineData("Don't!", "dont")]
    [InlineData("  La-la, ", "lala")]
    [InlineData("Ångström", "ångström")]
    [InlineData("!!!", "")]
    public void AWordIsComparedWithoutItsPunctuationAndCase(string word, string expected)
    {
        Assert.Equal(expected, LyricsRules.Core(word));
    }

    [Theory]
    [InlineData("vocals", true)]
    [InlineData("Vocal", true)]
    [InlineData("VOCALS", true)]
    [InlineData("vocalist", false)]
    [InlineData("drums", false)]
    [InlineData("", false)]
    public void TheVocalStemIsMatchedAsTheOldDawDidIt(string stem, bool vocal)
    {
        Assert.Equal(vocal, LyricsRules.IsVocalStem(stem));
    }

    [Fact]
    public void TimesAreRoundedToMillisecondsWithHalvesGoingUp()
    {
        Assert.Equal(1.234, LyricsRules.Milliseconds(1.23449));
        Assert.Equal(0.001, LyricsRules.Milliseconds(0.0005));
        Assert.Equal(0.7, LyricsRules.Milliseconds(0.7));
    }

    // ----- unsure words

    [Fact]
    public void ARunOfThreeCollapsedWordsIsFlaggedAndARunOfTwoIsNot()
    {
        ValueList<LyricWord> three = Words(("a", 0, 1), ("b", 1, 2), ("c", 2, 2.01), ("d", 2.01, 2.02), ("e", 2.02, 2.03), ("f", 3, 4));
        ValueList<LyricWord> two = Words(("a", 0, 1), ("c", 2, 2.01), ("d", 2.01, 2.02), ("f", 3, 4));

        Assert.Equal([2, 3, 4], Flagged(LyricsRules.MarkUnsure(three)));
        Assert.Empty(Flagged(LyricsRules.MarkUnsure(two)));
    }

    [Fact]
    public void EightWordsPackedIntoUnderNineTenthsOfASecondAreFlaggedAndEightSpreadOverMoreAreNot()
    {
        ValueList<LyricWord> packed = [.. Enumerable.Range(0, 8).Select(i => new LyricWord("w", i * 0.1, (i + 1) * 0.1, 0))];
        ValueList<LyricWord> spread = [.. Enumerable.Range(0, 8).Select(i => new LyricWord("w", i * 0.12, (i + 1) * 0.12, 0))];

        Assert.Equal(8, Flagged(LyricsRules.MarkUnsure(packed)).Length);
        Assert.Empty(Flagged(LyricsRules.MarkUnsure(spread)));
    }

    [Fact]
    public void TheFlagIsCarriedToTheEdgeOfASqueezeButNotIntoRealWordsThatFollowIt()
    {
        ValueList<LyricWord> words = Words(
            ("a", 0, 1), ("c1", 2, 2.01), ("c2", 2.01, 2.02), ("c3", 2.02, 2.03), ("x", 2.03, 2.53), ("c5", 2.53, 2.54), ("y", 3, 4), ("z", 4, 5));

        Assert.Equal([1, 2, 3, 4, 5], Flagged(LyricsRules.MarkUnsure(words)));
    }

    [Fact]
    public void EarlierFlagsAreClearedSoTheResultDependsOnTheTimesAlone()
    {
        ValueList<LyricWord> words = [new LyricWord("a", 0, 1, 0, true), new LyricWord("b", 1, 2, 0, true)];

        Assert.Empty(Flagged(LyricsRules.MarkUnsure(words)));
        Assert.All(LyricsRules.MarkUnsure(words), word => Assert.Null(word.Unsure));
    }

    // ----- lines

    [Fact]
    public void ARepeatedWordLandsOnTheLineWhereItWasWrittenAndAMissingWordKeepsTheLineBefore()
    {
        ValueList<LyricWord> words = Words(("la", 0, 1), ("la", 1, 2), ("hey", 2, 3), ("la", 3, 4), ("zzz", 4, 5));

        ValueList<LyricWord> placed = LyricsRules.AssignLines(words, ["la la", "hey la"]);

        Assert.Equal([0, 0, 1, 1, 1], placed.Select(word => word.Line));
    }

    // ----- from a model

    [Fact]
    public void AlignedWordsBecomeSourceSecondsAndTheirLinesAreFoundInTheText()
    {
        TimedWord[] heard = [new("hello", 0.5, 0.9), new("world", 1, 1.4), new("bye", 2, 2.5)];

        ClipLyrics lyrics = LyricsRules.FromAlignment("[Verse]\nhello world\nbye", heard, 10);

        Assert.Equal([10.5, 11, 12], lyrics.Words.Select(word => word.Start));
        Assert.Equal([10.9, 11.4, 12.5], lyrics.Words.Select(word => word.End));
        Assert.Equal([0, 0, 1], lyrics.Words.Select(word => word.Line));
        Assert.Equal(("aligned", null), (lyrics.Source, lyrics.Model));
        Assert.Equal("[Verse]\nhello world\nbye", lyrics.Text);
    }

    [Fact]
    public void BlankWordsAndTimesThatAreNotNumbersAreLeftOutAndAnEndBeforeItsStartIsRaised()
    {
        TimedWord[] heard = [new("  ", 0, 1), new(null, 0, 1), new("a", double.NaN, 1), new("b", 0, double.PositiveInfinity), new(" ok ", 3, 2)];

        ClipLyrics lyrics = LyricsRules.FromAlignment("ok", heard, 1, "custom", "model-x");

        Assert.Equal([new LyricWord("ok", 4, 4, 0)], lyrics.Words);
        Assert.Equal(("custom", "model-x"), (lyrics.Source, lyrics.Model));
    }

    [Fact]
    public void ATranscriptStartsANewLineAfterAPauseOrAfterEightWords()
    {
        TimedWord[] pause = [new("a", 0, 0.5), new("b", 0.5, 1), new("c", 2, 2.5), new("d", 2.5, 3)];
        TimedWord[] long10 = [.. Enumerable.Range(0, 10).Select(i => new TimedWord($"w{i}", i * 0.3, (i * 0.3) + 0.3))];

        ClipLyrics paused = LyricsRules.FromTranscript(pause, 0);
        ClipLyrics capped = LyricsRules.FromTranscript(long10, 0);

        Assert.Equal("a b\nc d", paused.Text);
        Assert.Equal([0, 0, 1, 1], paused.Words.Select(word => word.Line));
        Assert.Equal("w0 w1 w2 w3 w4 w5 w6 w7\nw8 w9", capped.Text);
        Assert.Equal(("stt", null), (paused.Source, paused.Model));
    }

    [Fact]
    public void ATwoHundredThousandWordTranscriptIsPlacedAndTimed()
    {
        TimedWord[] heard = [.. Enumerable.Range(0, 200_000).Select(i => new TimedWord("la", i * 0.5, (i * 0.5) + 0.4))];
        string text = string.Join('\n', Enumerable.Range(0, 25_000).Select(_ => "la la la la la la la la"));

        ClipLyrics aligned = LyricsRules.FromAlignment(text, heard, 0);
        ClipLyrics transcript = LyricsRules.FromTranscript(heard, 0);

        Assert.Equal(24_999, aligned.Words[^1].Line);
        Assert.Equal(200_000, transcript.Words.Count);
    }

    // ----- estimate

    [Fact]
    public void LinesShareTheSpanEquallyAndTheWordsOfALineShareItByLength()
    {
        ValueList<LyricWord> words = LyricsRules.Estimate("ab cd\nef", 0, 4);

        (string, double, double, int)[] expected = [("ab", 0.0, 1.0, 0), ("cd", 1.0, 2.0, 0), ("ef", 2.0, 3.2, 1)];
        Assert.Equal(expected, words.Select(word => (word.Word, word.Start, word.End, word.Line!.Value)));
    }

    [Fact]
    public void KnownLineStartsTimeEachLineToTheNextAndTheLastToTheEnd()
    {
        ValueList<LyricWord> words = LyricsRules.Estimate("ab\ncd", 1, 6, [1, 3]);

        Assert.Equal([(1.0, 2.2), (3.0, 4.2)], words.Select(word => (word.Start, word.End)));
    }

    [Theory]
    [InlineData("", 0, 4)]
    [InlineData("[Verse]", 0, 4)]
    [InlineData("la", 4, 4)]
    [InlineData("la", 5, 4)]
    public void NothingIsEstimatedWithoutWordsOrASpan(string text, double start, double end)
    {
        Assert.Empty(LyricsRules.Estimate(text, start, end));
    }

    // ----- editing

    [Fact]
    public void ANewTextKeepsTheWordTimesWhenOnlySpacingCasePunctuationOrTagsChanged()
    {
        ClipLyrics lyrics = Lyrics("la la", [new LyricWord("la", 0.5, 0.9, 0), new LyricWord("la", 1, 1.4, 0, true)]);

        ClipLyrics edited = LyricsRules.SetText(lyrics, "[Chorus]\nLa,\nla!");

        Assert.Equal(["La,", "la!"], edited.Words.Select(word => word.Word));
        Assert.Equal([0.5, 1.0], edited.Words.Select(word => word.Start));
        Assert.Equal([true], edited.Words.Select(word => word.Unsure).OfType<bool>());
        Assert.Equal([0, 1], edited.Words.Select(word => word.Line!.Value));
        Assert.Equal("manual", edited.Source);
    }

    [Fact]
    public void ADifferentWordDropsTheTimesAndGeneratedLyricsStayGenerated()
    {
        ClipLyrics typed = Lyrics("la la", [new LyricWord("la", 0, 1, 0), new LyricWord("la", 1, 2, 0)], "aligned");
        ClipLyrics generated = typed with { Source = "generated", Model = "m" };

        ClipLyrics typedEdit = LyricsRules.SetText(typed, "la la la");
        ClipLyrics generatedEdit = LyricsRules.SetText(generated, "la");

        Assert.Empty(typedEdit.Words);
        Assert.Equal("manual", typedEdit.Source);
        Assert.Equal(("generated", "m"), (generatedEdit.Source, generatedEdit.Model));
    }

    [Fact]
    public void ShiftingMovesEveryWordAndNeverBringsTheFirstBeforeZero()
    {
        ClipLyrics lyrics = Lyrics("a b", [new LyricWord("a", 0.5, 1, 0), new LyricWord("b", 2, 3, 0, true)]);

        ClipLyrics later = LyricsRules.Shift(lyrics, 0.25);
        ClipLyrics earlier = LyricsRules.Shift(lyrics, -5);

        Assert.Equal([(0.75, 1.25), (2.25, 3.25)], later.Words.Select(word => (word.Start, word.End)));
        Assert.Equal([(0.0, 0.5), (1.5, 2.5)], earlier.Words.Select(word => (word.Start, word.End)));
        Assert.Equal(true, earlier.Words[1].Unsure);
        Assert.Equal(lyrics, LyricsRules.Shift(lyrics with { Words = [] }, 3) with { Words = lyrics.Words });
    }

    [Fact]
    public void RebasingDropsWordsThatEndBeforeTheNewStartAndCutsOneThatStraddlesIt()
    {
        ClipLyrics lyrics = Lyrics("a b c", [new LyricWord("a", 0, 1, 0), new LyricWord("b", 1, 2, 0, true), new LyricWord("c", 2, 3, 0)]);

        ClipLyrics stem = LyricsRules.Rebase(lyrics, -1.5);

        Assert.Equal([("b", 0.0, 0.5), ("c", 0.5, 1.5)], stem.Words.Select(word => (word.Word, word.Start, word.End)));
        Assert.Equal(true, stem.Words[0].Unsure);
        Assert.Equal(lyrics.Words, LyricsRules.Rebase(lyrics, 0).Words);
    }

    [Fact]
    public void AWordTimeIsKeptBetweenItsNeighboursAndOrderedAndClearsTheFlag()
    {
        ClipLyrics lyrics = Lyrics("a b c", [new LyricWord("a", 0, 1, 0), new LyricWord("b", 1.5, 2, 0, true), new LyricWord("c", 3, 4, 0)]);

        ClipLyrics early = LyricsRules.SetWordTime(lyrics, 1, 0.2, null);
        ClipLyrics late = LyricsRules.SetWordTime(lyrics, 1, 5, null);
        ClipLyrics wide = LyricsRules.SetWordTime(lyrics, 1, null, 9);
        ClipLyrics both = LyricsRules.SetWordTime(lyrics, 1, 4, 5);
        ClipLyrics first = LyricsRules.SetWordTime(lyrics, 0, -3, 99);

        Assert.Equal((1.0, 2.0), (early.Words[1].Start, early.Words[1].End));
        Assert.Equal((2.0, 2.0), (late.Words[1].Start, late.Words[1].End));
        Assert.Equal((1.5, 3.0), (wide.Words[1].Start, wide.Words[1].End));
        Assert.Equal((3.0, 3.0), (both.Words[1].Start, both.Words[1].End));
        Assert.Equal((0.0, 1.5), (first.Words[0].Start, first.Words[0].End));
        Assert.Null(early.Words[1].Unsure);
        Assert.Equal(lyrics.Words[0], early.Words[0]);
    }

    [Fact]
    public void AStartAndAnEndTogetherGiveTheSameResultWhereverTheWordWasAndWhenRepeated()
    {
        ClipLyrics at12 = Lyrics("a b c", [new LyricWord("a", 0, 1, 0), new LyricWord("b", 1, 2, 0), new LyricWord("c", 2, 10, 0)]);
        ClipLyrics elsewhere = at12 with { Words = [at12.Words[0], new LyricWord("b", 1.2, 1.9, 0), at12.Words[2]] };

        ClipLyrics once = LyricsRules.SetWordTime(at12, 1, 4, 5);
        ClipLyrics other = LyricsRules.SetWordTime(elsewhere, 1, 4, 5);
        ClipLyrics twice = LyricsRules.SetWordTime(once, 1, 4, 5);

        Assert.Equal((2.0, 2.0), (once.Words[1].Start, once.Words[1].End));
        Assert.Equal(once.Words[1], other.Words[1]);
        Assert.Equal(once.Words[1], twice.Words[1]);
        Assert.Equal(once, twice);
    }

    [Fact]
    public void NeighboursThatOverlapNeverMakeAWordTimeThrowOrEndBeforeItsStart()
    {
        ClipLyrics lyrics = Lyrics("a b", [new LyricWord("a", 0, 3, 0), new LyricWord("b", 1, 2, 0)]);

        ClipLyrics moved = LyricsRules.SetWordTime(lyrics, 1, 1.5, null);
        ClipLyrics both = LyricsRules.SetWordTime(lyrics, 0, 2, 0.5);

        Assert.True(moved.Words[1].Start <= moved.Words[1].End);
        Assert.True(both.Words[0].Start <= both.Words[0].End);
    }

    // ----- text, timing and shifting

    [Fact]
    public void IdenticalTextKeepsTheTimesEvenWhenSomeHeardWordsAreNotInIt()
    {
        ClipLyrics aligned = Lyrics(
            "hello world",
            [new LyricWord("hello", 0, 1, 0), new LyricWord("world", 1, 2, 0), new LyricWord("oops", 2, 3, 0, true)],
            "aligned") with
        { Model = "m" };

        ClipLyrics edited = LyricsRules.SetText(aligned, "Hello,\nWORLD!");

        Assert.Equal([0.0, 1.0, 2.0], edited.Words.Select(word => word.Start));
        Assert.Equal(["hello", "world", "oops"], edited.Words.Select(word => word.Word));
        Assert.Equal(("aligned", "m", "Hello,\nWORLD!"), (edited.Source, edited.Model, edited.Text));
        Assert.Equal(aligned, LyricsRules.SetText(aligned, aligned.Text));
    }

    [Fact]
    public void DroppingTheTimesDropsWhatDescribedThemAndGeneratedLyricsKeepTheirModel()
    {
        ExtraFields extra = ExtraFields.FromJson(("alignedBy", "1"));
        ValueList<LyricWord> timed = [new LyricWord("la", 0, 1, 0), new LyricWord("la", 1, 2, 0)];
        ClipLyrics typed = Lyrics("la la", timed, "aligned") with { Model = "aligner", Extra = extra };
        ClipLyrics generated = typed with { Source = "generated", Model = "writer" };

        ClipLyrics typedEdit = LyricsRules.SetText(typed, "la la la");
        ClipLyrics generatedEdit = LyricsRules.SetText(generated, "la la la");

        Assert.Equal(("manual", null, true), (typedEdit.Source, typedEdit.Model, typedEdit.Extra.IsEmpty));
        Assert.Equal(("generated", "writer", true), (generatedEdit.Source, generatedEdit.Model, generatedEdit.Extra.IsEmpty));
        Assert.Equal(typedEdit, LyricsRules.ClearTiming(typed) with { Text = "la la la" });
        Assert.Equal(("generated", "writer"), (LyricsRules.ClearTiming(generated).Source, LyricsRules.ClearTiming(generated).Model));
    }

    [Fact]
    public void ClearingTheTimingOfLyricsThatHaveNoneReturnsThemAsTheyWere()
    {
        ClipLyrics untimed = Lyrics("la", [], "aligned") with { Model = "m" };

        Assert.Equal(untimed, LyricsRules.ClearTiming(untimed));
    }

    [Fact]
    public void ShiftingByNothingIsANoOpEvenForTimesThatAreNotRoundedToMilliseconds()
    {
        ClipLyrics lyrics = Lyrics("a", [new LyricWord("a", 0.123456, 0.654321, 0)]);

        Assert.Equal(lyrics, LyricsRules.Shift(lyrics, 0));
        Assert.Equal(lyrics, LyricsRules.Shift(lyrics with { Words = [new LyricWord("a", 0, 0.654321, 0)] }, -3) with { Words = lyrics.Words });
    }

    [Fact]
    public void AByteOrderMarkInFrontOfAnLrcFileDoesNotCostItsFirstStamp()
    {
        IReadOnlyList<LrcLine> lines = LyricsRules.ParseLrc("\uFEFF[00:01.00]hello\n[00:02.00]world", out _);

        Assert.Equal(["hello", "world"], lines.Select(line => line.Text));
    }

    // ----- hostile input

    [Fact]
    public void ALineWithManyStampsAndALongBodyIsCutShortBeforeItsTextIsBuilt()
    {
        string stamps = string.Concat(Enumerable.Repeat("[0:0]", 2000));
        string file = stamps + new string('a', 900_000);

        IReadOnlyList<LrcLine> lines = LyricsRules.ParseLrc(file, out bool tooLarge);

        Assert.True(tooLarge);
        Assert.Empty(lines);
    }

    [Fact]
    public void ALineOfAHundredThousandStampsIsRead()
    {
        string file = string.Concat(Enumerable.Repeat("[0:01.5]", 100_000)) + "la";

        IReadOnlyList<LrcLine> lines = LyricsRules.ParseLrc(file, out bool tooLarge);

        Assert.False(tooLarge);
        Assert.Equal(100_000, lines.Count);
    }

    [Fact]
    public void ManyLinesOfShortWordsFillTheLimitAndNoFurther()
    {
        string stamps = string.Concat(Enumerable.Repeat("[0:0]", 3000));
        string file = stamps + string.Join(' ', Enumerable.Repeat("a", 200_000));

        Assert.Empty(LyricsRules.ParseLrc(file, out bool tooLarge));
        Assert.True(tooLarge);
    }

    [Fact]
    public void ASpentSearchBudgetLeavesTheRestOfTheWordsOnTheLineTheyAreOn()
    {
        string[] lines = [string.Concat(Enumerable.Repeat("ab", 1000)), "zz"];
        LyricWord[] misses = [.. Enumerable.Range(0, 50).Select(i => new LyricWord($"abx{i}", i, i + 0.5, 0))];
        ValueList<LyricWord> words = [.. misses, new LyricWord("zz", 60, 61, 0)];

        ValueList<LyricWord> spent = LyricsRules.AssignLines(words, lines, 100_000);
        ValueList<LyricWord> whole = LyricsRules.AssignLines(words, lines);

        Assert.Equal(0, spent[^1].Line);
        Assert.Equal(1, whole[^1].Line);
    }

    [Fact]
    public void ADistinctWordThatIsNeverFoundIsChargedItsLengthTimesTheTextItMayCompare()
    {
        string[] lines = [string.Concat(Enumerable.Repeat("ab", 500_000)), "zz"];
        ValueList<LyricWord> words =
        [
            .. Enumerable.Range(0, 5000).Select(i => new LyricWord($"{new string('a', 20)}{i}", i, i + 0.5, 0)),
            new LyricWord("zz", 6000, 6001, 0),
        ];

        // 1,000,000 characters at 21 or more a word is 2.1e7 a miss: the default budget of 1e9 is gone after fewer than 50 misses,
        // so the last word, 4,950 words later, is not looked for and keeps its line.
        ValueList<LyricWord> placed = LyricsRules.AssignLines(words, lines);

        Assert.Equal(0, placed[^1].Line);
        Assert.Equal(5001, placed.Count);
    }

    [Fact]
    public void AMissIsRememberedSoRepeatingItCostsNothing()
    {
        string[] lines = [string.Concat(Enumerable.Repeat("ab", 1000)), "zz"];
        ValueList<LyricWord> words = [.. Enumerable.Repeat(new LyricWord("nothere", 0, 1, 0), 2000), new LyricWord("zz", 2, 3, 0)];

        // One miss costs about 14,000; a budget for four of them still reaches the last word because the other 1,999 are free.
        ValueList<LyricWord> placed = LyricsRules.AssignLines(words, lines, 60_000);

        Assert.Equal(1, placed[^1].Line);
    }

    // ----- LRC

    [Fact]
    public void AnLrcFileIsReadByTimeWithTagsWordMarksAndEmptyLinesLeftOut()
    {
        string file = "[ar:Someone]\n[00:12.50]Hello <00:12.80>world\r\n[01:02.3][00:05]Again\n[00:30.5]\nnoise";

        IReadOnlyList<LrcLine> lines = LyricsRules.ParseLrc(file, out bool tooLarge);

        Assert.Equal(["Again", "Hello world", "Again"], lines.Select(line => line.Text));
        Assert.Equal([5, 12.5, 62.3], lines.Select(line => line.Time), new TolerantComparer());
        Assert.False(tooLarge);
        Assert.Empty(LyricsRules.ParseLrc(null, out _));
        Assert.True(LyricsRules.ParseLrc("[99999999999999999999:00]x", out _).Single().Time > 1e20);
        Assert.Empty(LyricsRules.ParseLrc("[" + new string('9', 400) + ":00]x", out _));
    }

    [Fact]
    public void LrcLinesAreTimedByEstimateFromTheirOwnStartAndTheLastRunsForTheGivenTime()
    {
        ClipLyrics? lyrics = Lrc("[00:01.00]one two\n[00:03.00]three", time => time, 2);

        Assert.Equal("one two\nthree", lyrics!.Text);
        Assert.Equal([(1.0, 2.0), (2.0, 3.0), (3.0, 4.2)], lyrics.Words.Select(word => (word.Start, word.End)));
        Assert.Equal("lrc", lyrics.Source);
    }

    [Fact]
    public void LrcLinesThatStartBeforeTheSourceBeginsAreCutAndBareTagsAreNotSungLines()
    {
        ClipLyrics? lyrics = Lrc("[00:00.00][Chorus]\n[00:02.00]one two\n[00:04.00]x", time => time - 3, 2);

        Assert.NotNull(lyrics);
        Assert.Equal("one two\nx", lyrics.Text);
        Assert.Equal(["two", "x"], lyrics.Words.Select(word => word.Word));
        Assert.Equal(0.0, lyrics.Words[0].Start);
        Assert.Null(Lrc("just words\n[ti:Title]", time => time));
    }

    // ----- links

    [Fact]
    public void AParentThatLeadsBackToTheClipWouldMakeALoopAndOneThatDoesNotWouldNot()
    {
        AudioClip Clip(string id, string? parent)
        {
            ScoreMeta? score = parent is null ? null : new ScoreMeta { Parent = ClipId.From(parent) };
            return new AudioClip
            {
                Id = ClipId.From(id),
                Name = id,
                Media = MediaId.From("med_1"),
                Start = 0,
                Duration = 1,
                Offset = 0,
                TrimEnd = 0,
                Gain = 1,
                FadeIn = 0,
                FadeOut = 0,
                Muted = false,
                Meta = score is null ? null : new ClipMeta { Score = score },
            };
        }
        AudioClip[] clips = [Clip("a", "b"), Clip("b", "c"), Clip("c", null), Clip("x", null), Clip("p", "q"), Clip("q", "p")];
        Track track = BlankProject.Track("trk_1") with { Clips = [.. clips] };
        Track[] tracks = [track];

        Assert.True(ClipLinks.ParentLoops(tracks, ClipId.From("c"), ClipId.From("a")));
        Assert.True(ClipLinks.ParentLoops(tracks, ClipId.From("a"), ClipId.From("a")));
        Assert.False(ClipLinks.ParentLoops(tracks, ClipId.From("a"), ClipId.From("c")));
        Assert.False(ClipLinks.ParentLoops(tracks, ClipId.From("x"), ClipId.From("p")));
    }
}
