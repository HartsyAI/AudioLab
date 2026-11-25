using AudioLab.Web.Session;

namespace AudioLab.Web.Tests;

/// <summary>The swing and step count typed into the drums tab: only what Core would accept is sent.</summary>
public sealed class BeatDraftTests
{
    /// <summary>A swing from 0 to 1 is read; anything outside, or not a number, is not.</summary>
    [Theory]
    [InlineData("0", true)]
    [InlineData("0.6", true)]
    [InlineData("1", true)]
    [InlineData("1.01", false)]
    [InlineData("-0.1", false)]
    [InlineData("", false)]
    [InlineData("swing", false)]
    public void SwingIsReadOnlyWithinZeroToOne(string text, bool valid)
    {
        bool ok = BeatDraft.TryParseSwing(text, out double swing);

        Assert.Equal(valid, ok);
        Assert.True(!ok || swing is >= 0 and <= 1);
    }

    /// <summary>A step count from 1 to 256 is read; zero, a fraction, a sign and a count past the limit are not.</summary>
    [Theory]
    [InlineData("1", true)]
    [InlineData("16", true)]
    [InlineData("256", true)]
    [InlineData("0", false)]
    [InlineData("257", false)]
    [InlineData("16.5", false)]
    [InlineData("-4", false)]
    [InlineData("", false)]
    public void StepsAreWholeNumbersFromOneToTheLimit(string text, bool valid)
    {
        Assert.Equal(valid, BeatDraft.TryParseSteps(text, out _));
    }
}
