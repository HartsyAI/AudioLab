using System.Globalization;
using AudioLab.Core.Rules;
using AudioLab.Web.Session.Effects;

namespace AudioLab.Web.Tests;

/// <summary>How an effect setting's value is written beside its knob.</summary>
public sealed class EffectValueTextTests
{
    private static EffectParameter Setting(EffectKind kind, string name) => EffectRules.Parameters(kind).Single(setting => setting.Name == name);

    /// <summary>Each unit is written the way a musician reads it.</summary>
    [Fact]
    public void EachUnitIsWrittenTheWayAMusicianReadsIt()
    {
        Assert.Equal("90 Hz", EffectValueText.Of(Setting(EffectKind.Eq, "hpFreq"), 90));
        Assert.Equal("1 kHz", EffectValueText.Of(Setting(EffectKind.Eq, "midFreq"), 1000));
        Assert.Equal("4.25 kHz", EffectValueText.Of(Setting(EffectKind.Eq, "highFreq"), 4250));
        Assert.Equal("+2.5 dB", EffectValueText.Of(Setting(EffectKind.Eq, "midGain"), 2.5));
        Assert.Equal("-12 dB", EffectValueText.Of(Setting(EffectKind.Eq, "lowGain"), -12));
        Assert.Equal("-24 dB", EffectValueText.Of(Setting(EffectKind.Compressor, "threshold"), -24));
        Assert.Equal("6 dB", EffectValueText.Of(Setting(EffectKind.Compressor, "makeup"), 6));
        Assert.Equal("4:1", EffectValueText.Of(Setting(EffectKind.Compressor, "ratio"), 4));
        Assert.Equal("3 ms", EffectValueText.Of(Setting(EffectKind.Compressor, "attack"), 0.003));
        Assert.Equal("250 ms", EffectValueText.Of(Setting(EffectKind.Compressor, "release"), 0.25));
        Assert.Equal("3 s", EffectValueText.Of(Setting(EffectKind.Reverb, "decay"), 3));
        Assert.Equal("30%", EffectValueText.Of(Setting(EffectKind.Reverb, "mix"), 0.3));
        Assert.Equal("90%", EffectValueText.Of(Setting(EffectKind.Delay, "feedback"), 0.9));
        Assert.Equal("1.5", EffectValueText.Of(Setting(EffectKind.Eq, "midQ"), 1.5));
    }

    /// <summary>The unit is chosen after rounding, so a value that rounds up to a thousand is written in the larger unit.</summary>
    [Fact]
    public void TheUnitIsChosenAfterRounding()
    {
        Assert.Equal("1 kHz", EffectValueText.Of(Setting(EffectKind.Eq, "midFreq"), 999.96));
        Assert.Equal("999.9 Hz", EffectValueText.Of(Setting(EffectKind.Eq, "midFreq"), 999.94));
        Assert.Equal("1 s", EffectValueText.Of(Setting(EffectKind.Compressor, "release"), 0.99996));
        Assert.Equal("999.9 ms", EffectValueText.Of(Setting(EffectKind.Compressor, "release"), 0.99994));
    }

    /// <summary>A value that rounds to nothing is written as 0, never -0.</summary>
    [Fact]
    public void AValueThatRoundsToNothingIsNeverMinusZero()
    {
        Assert.Equal("0 dB", EffectValueText.Of(Setting(EffectKind.Eq, "lowGain"), -0.04));
        Assert.Equal("0 ms", EffectValueText.Of(Setting(EffectKind.Reverb, "predelay"), 0));
        Assert.Equal("0%", EffectValueText.Of(Setting(EffectKind.Saturation, "mix"), 0.001));
    }

    /// <summary>Every setting of every kind is written at both ends of its range without a sign mistake or an empty text.</summary>
    [Fact]
    public void EverySettingIsWrittenAtBothEnds()
    {
        foreach (EffectKind kind in Enum.GetValues<EffectKind>())
        {
            foreach (EffectParameter setting in EffectRules.Parameters(kind))
            {
                Assert.False(string.IsNullOrWhiteSpace(EffectValueText.Of(setting, setting.Min)));
                Assert.False(string.IsNullOrWhiteSpace(EffectValueText.Of(setting, setting.Max)));
            }
        }
    }

    /// <summary>A number reads with a point and no grouping whatever language the browser is set to.</summary>
    [Fact]
    public void ANumberReadsWithAPointUnderGerman()
    {
        CultureInfo before = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("de-DE");
        try
        {
            Assert.Equal("4.25 kHz", EffectValueText.Of(Setting(EffectKind.Eq, "highFreq"), 4250));
            Assert.Equal("+2.5 dB", EffectValueText.Of(Setting(EffectKind.Eq, "midGain"), 2.5));
            Assert.Equal("1.5 ms", EffectValueText.Of(Setting(EffectKind.Compressor, "attack"), 0.0015));
        }
        finally
        {
            CultureInfo.CurrentCulture = before;
        }
    }
}
