using AudioLab.Web.Session;

namespace AudioLab.Web.Tests;

/// <summary>The gain typed into a lane's field: it reads from 0 up to the most a lane may have, and it names the dot.</summary>
public sealed class GainDraftTests
{
    /// <summary>The gains a lane takes, and the ones it refuses before anything is sent.</summary>
    [Theory]
    [InlineData("0", true, 0.0)]
    [InlineData("1", true, 1.0)]
    [InlineData("16", true, 16.0)]
    [InlineData(" 0.5 ", true, 0.5)]
    [InlineData("16.5", false, 0.0)]
    [InlineData("-1", false, 0.0)]
    [InlineData("1,5", false, 0.0)]
    [InlineData("", false, 0.0)]
    public void TheGainReadsFromZeroToTheMostALaneMayHave(string text, bool accepted, double value)
    {
        Assert.Equal(accepted, BeatDraft.TryParseGain(text, out double gain));
        if (accepted)
        {
            Assert.Equal(value, gain);
        }
    }

    /// <summary>A gain written and read back is the same value, so the field does not drift.</summary>
    [Fact]
    public void AGainWrittenReadsBackExactly()
    {
        double gain = 0.1 + 0.2;

        Assert.True(BeatDraft.TryParseGain(BeatDraft.FormatGain(gain), out double back));
        Assert.Equal(gain, back);
    }

    /// <summary>Set gain has something to send only when the typed gain differs from the lane's.</summary>
    [Fact]
    public void TheSameGainSendsNothing()
    {
        Assert.False(BeatDraft.CanApplyGain("1", 1));
        Assert.True(BeatDraft.CanApplyGain("1.5", 1));
    }

    /// <summary>A comma is named as the usual slip, so the person is told the dot is wanted.</summary>
    [Fact]
    public void ACommaIsNamedAsTheDecimalPoint()
    {
        Assert.Contains("dot", BeatDraft.GainReason("1,5"), StringComparison.Ordinal);
    }
}
