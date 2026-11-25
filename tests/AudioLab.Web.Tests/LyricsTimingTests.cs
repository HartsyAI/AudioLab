using AudioLab.Core.Model;
using AudioLab.Core.Rules;
using AudioLab.Web.Session;

namespace AudioLab.Web.Tests;

/// <summary>Holds the lyrics timing tests: the word under the playhead, and the word-time moves the lyrics tab asks for.</summary>
public sealed class LyricsTimingTests
{
    private static readonly ClipLyrics _timed = new()
    {
        Text = "one two three",
        Words =
        [
            new LyricWord("one", 1, 2),
            new LyricWord("two", 2.5, 3),
            new LyricWord("three", 4, 5),
        ],
    };

    /// <summary>Between two words, and before the first or after the last, no word is sounding.</summary>
    [Theory]
    [InlineData(0.5, null)]
    [InlineData(1, 0)]
    [InlineData(1.9, 0)]
    [InlineData(2.2, null)]
    [InlineData(2.5, 1)]
    [InlineData(4.99, 2)]
    [InlineData(5, null)]
    public void TheWordAtAPlaceIsTheOneThatSoundsThere(double source, int? expected)
    {
        Assert.Equal(expected, LyricsTiming.WordAt(_timed.Words, source));
    }

    /// <summary>A timeline position maps to the source second of the clip, and a position outside the clip maps to nothing.</summary>
    [Fact]
    public void ThePlayheadMapsToTheSourceOnlyInsideTheVisiblePart()
    {
        AudioClip clip = new()
        {
            Id = ClipId.From("a"),
            Name = "a",
            Media = MediaId.From("m"),
            Start = 10,
            Duration = 8,
            Offset = 2,
            TrimEnd = 1,
            Gain = 1,
            FadeIn = 0,
            FadeOut = 0,
            Muted = false,
        };
        Assert.Equal(3, LyricsTiming.SourceAt(clip, 11));
        Assert.Null(LyricsTiming.SourceAt(clip, 9.9));
        Assert.Null(LyricsTiming.SourceAt(clip, 15));
    }

    /// <summary>A word that moves into free time is not held, and one pushed into its neighbour says where it was held.</summary>
    [Fact]
    public void AWordHeldByANeighbourSaysWhere()
    {
        Assert.Null(LyricsTiming.HeldText(_timed, 1, 2.6, 2.9));
        string? held = LyricsTiming.HeldText(_timed, 1, 1.5, 2.9);
        Assert.NotNull(held);
        Assert.Contains("2.000", held, StringComparison.Ordinal);
    }

    /// <summary>A start dragged past its own end is held by that end, and the text says so rather than naming a neighbour.</summary>
    [Fact]
    public void AStartPastItsOwnEndSaysItCannotStartAfterItEnds()
    {
        Assert.Equal("A word cannot start after it ends.", LyricsTiming.HeldText(_timed, 1, 3.5, null));
    }

    /// <summary>An end dragged before its start is held by that start.</summary>
    [Fact]
    public void AnEndBeforeItsStartSaysItCannotEndBeforeItStarts()
    {
        Assert.Equal("A word cannot end before it starts.", LyricsTiming.HeldText(_timed, 1, null, 2.2));
    }

    /// <summary>A whole word dragged into its neighbour keeps its length: the move is clamped as a whole, so the end follows the start.</summary>
    [Fact]
    public void AMoveIntoTheWordBeforeKeepsTheLength()
    {
        double length = _timed.Words[1].End - _timed.Words[1].Start;
        (double? start, double? end) = LyricsTiming.Targets(_timed, 1, LyricWordPart.Body, -1);
        LyricWord kept = LyricsTiming.Preview(_timed, 1, start, end);
        Assert.Equal(2, kept.Start);
        Assert.Equal(length, kept.End - kept.Start, 3);
        Assert.NotNull(LyricsTiming.MoveHeldText(_timed, 1, -1));
    }

    /// <summary>The same holds for a move into the word after: its end is held and its start moves with it, never shorter.</summary>
    [Fact]
    public void AMoveIntoTheWordAfterKeepsTheLength()
    {
        double length = _timed.Words[1].End - _timed.Words[1].Start;
        (double? start, double? end) = LyricsTiming.Targets(_timed, 1, LyricWordPart.Body, 5);
        LyricWord kept = LyricsTiming.Preview(_timed, 1, start, end);
        Assert.Equal(4 - length, kept.Start, 3);
        Assert.Equal(4, kept.End);
        Assert.Equal(length, kept.End - kept.Start, 3);
    }

    /// <summary>Repeated key moves into a neighbour stop at the neighbour and never collapse the word.</summary>
    [Fact]
    public void RepeatedKeyMovesIntoTheWordBeforeDoNotCollapseIt()
    {
        ClipLyrics lyrics = _timed;
        for (int step = 0; step < 20; step++)
        {
            (double? start, double? end) = LyricsTiming.Targets(lyrics, 1, LyricWordPart.Body, -LyricsTiming.Nudge);
            lyrics = LyricsRules.SetWordTime(lyrics, 1, start, end);
        }
        Assert.Equal(2, lyrics.Words[1].Start);
        Assert.Equal(2.5, lyrics.Words[1].End, 3);
    }

    /// <summary>A word of the lyrics is the same word while it has the same text and times; a row whose index names another word is not.</summary>
    [Fact]
    public void ARowNoLongerNamingItsWordIsNotTheSameWord()
    {
        LyricWord began = _timed.Words[2];
        Assert.True(LyricsTiming.SameWord(_timed, 2, began));
        ClipLyrics shorter = _timed with { Words = [_timed.Words[0], _timed.Words[1]] };
        Assert.False(LyricsTiming.SameWord(shorter, 2, began));
        Assert.False(LyricsTiming.SameWord(_timed, 2, new LyricWord("three", 4, 4.5)));
    }
}
