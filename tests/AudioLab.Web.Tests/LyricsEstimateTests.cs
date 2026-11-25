using AudioLab.Web.Session;

namespace AudioLab.Web.Tests;

/// <summary>Holds the estimate tests: the estimate is disabled with its reason while the text is unsaved, and its words are spelled for the count.</summary>
public sealed class LyricsEstimateTests
{
    /// <summary>While the editor holds text that is not saved, the estimate is disabled and says to save the text first.</summary>
    [Fact]
    public void WhileTheTextIsUnsavedTheEstimateSaysToSaveFirst()
    {
        Assert.Equal("Save the text first, so the estimate spreads the words you see.", LyricsEstimate.Block(true, null, unsaved: true, words: 300));
    }

    /// <summary>With saved words and no unsaved text the estimate is allowed; with no words it says to write some.</summary>
    [Fact]
    public void TheEstimateIsAllowedWithSavedWords()
    {
        Assert.Null(LyricsEstimate.Block(true, null, unsaved: false, words: 300));
        Assert.Equal("Write some lyrics first; there are no words to spread.", LyricsEstimate.Block(true, null, unsaved: false, words: 0));
    }

    /// <summary>A project that cannot be edited gives its own reason.</summary>
    [Fact]
    public void AProjectThatCannotBeEditedGivesItsReason()
    {
        Assert.Equal("Reconnecting to the server.", LyricsEstimate.Block(false, "Reconnecting to the server.", unsaved: false, words: 3));
    }

    /// <summary>The status line spells the word for one and the words for more.</summary>
    [Theory]
    [InlineData(1, "Spread 1 word evenly over 120.0 seconds from 0.0 s.")]
    [InlineData(300, "Spread 300 words evenly over 120.0 seconds from 0.0 s.")]
    public void TheSummarySpellsTheCount(int words, string expected)
    {
        Assert.Equal(expected, LyricsEstimate.Summary(words, 120, 0));
    }
}
