using AudioLab.Core.Model;
using AudioLab.Web.Services;
using AudioLab.Web.Session;

namespace AudioLab.Web.Tests;

/// <summary>Holds the lyrics cursor tests: the word under the playhead is announced only when it changes, so each word redraws alone.</summary>
public sealed class LyricsCursorTests
{
    private static readonly ClipLyrics _lyrics = new()
    {
        Text = "one two three",
        Words =
        [
            new LyricWord("one", 1, 2),
            new LyricWord("two", 2.5, 3),
            new LyricWord("three", 4, 5),
        ],
    };

    private static readonly AudioClip _clip = new()
    {
        Id = ClipId.From("a"),
        Name = "a",
        Media = MediaId.From("m"),
        Start = 0,
        Duration = 10,
        Offset = 0,
        TrimEnd = 0,
        Gain = 1,
        FadeIn = 0,
        FadeOut = 0,
        Muted = false,
    };

    /// <summary>The cursor says so once per word it moves to, never for a move inside the same word or to the gap between words.</summary>
    [Fact]
    public void TheCursorSaysSoOnlyWhenTheWordUnderThePlayheadChanges()
    {
        using PlaybackService playback = new(
            new FakeAudioHost(),
            new HttpClient { BaseAddress = new Uri("http://example.test/audiolab/") },
            TimeProvider.System);
        using LyricsCursor cursor = new(playback);
        int said = 0;
        cursor.Changed += () => said++;

        cursor.Follow(_clip, _lyrics);
        Assert.Null(cursor.Index);
        Assert.Equal(1, said);

        playback.Seek(1.5);
        Assert.Equal(0, cursor.Index);
        Assert.Equal(2, said);

        playback.Seek(1.7);
        Assert.Equal(2, said);

        playback.Seek(2.2);
        Assert.Null(cursor.Index);
        Assert.Equal(3, said);

        playback.Seek(2.6);
        Assert.Equal(1, cursor.Index);
        Assert.Equal(4, said);
    }
}
