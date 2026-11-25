using AudioLab.Core.Model;
using AudioLab.Web.Session;

namespace AudioLab.Web.Tests;

/// <summary>Holds the lyrics timing tests: the word under the playhead and the word-time preview.</summary>
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
        Assert.Equal("A word cannot end before it starts.", LyricsTiming.HeldText(_timed, 1, null, 2.2));
    }

    /// <summary>The preview is Core's own rule, so a start past the word's end is kept where Core keeps it.</summary>
    [Fact]
    public void ThePreviewIsCoresRuleForTheWord()
    {
        LyricWord kept = LyricsTiming.Preview(_timed, 1, 1.5, 2.9);
        Assert.Equal(2, kept.Start);
        Assert.Equal(2.9, kept.End);
    }
}
