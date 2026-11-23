using AudioLab.Audio.Tests.Support;
using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Audio.Tests;

public sealed class EqTests
{
    private static EqEffect Flat() => (EqEffect)EffectRules.Create(EffectKind.Eq, EffectId.From("eq"));

    [Fact]
    public void AFlatEqualiserPassesTheMiddleOfTheSpectrumUntouched()
    {
        Assert.Equal(0, EffectRig.GainDb(Flat(), 1000), 1);
        Assert.Equal(0, EffectRig.GainDb(Flat(), 5000), 1);
    }

    [Fact]
    public void APeakGivesItsGainExactlyAtItsCentre()
    {
        EqEffect eq = Flat() with { MidGain = 6, MidFreq = 1000, MidQ = 1 };

        Assert.Equal(6, EffectRig.GainDb(eq, 1000), 1);
        Assert.True(Math.Abs(EffectRig.GainDb(eq, 100)) < 0.5);
    }

    [Fact]
    public void APeakCanCut()
    {
        Assert.Equal(-9, EffectRig.GainDb(Flat() with { MidGain = -9, MidFreq = 2000, MidQ = 2 }, 2000), 1);
    }

    [Fact]
    public void TheHighPassIsASecondOrderFilterWhoseCornerIsItsResonanceInDecibels()
    {
        EqEffect eq = Flat() with { HpFreq = 1000 };

        Assert.Equal(0.707, EffectRig.GainDb(eq, 1000), 1);
        Assert.True(EffectRig.GainDb(eq, 100) < -35);
        Assert.True(Math.Abs(EffectRig.GainDb(eq, 8000)) < 0.5);
    }

    [Fact]
    public void TheLowPassIsASecondOrderFilterWhoseCornerIsItsResonanceInDecibels()
    {
        EqEffect eq = Flat() with { LpFreq = 1000 };

        Assert.Equal(0.707, EffectRig.GainDb(eq, 1000), 1);
        Assert.True(EffectRig.GainDb(eq, 10000) < -35);
        Assert.True(Math.Abs(EffectRig.GainDb(eq, 100)) < 0.5);
    }

    [Fact]
    public void TheLowShelfLiftsTheBottomAndLeavesTheTop()
    {
        EqEffect eq = Flat() with { LowGain = 12, LowFreq = 200 };

        // A shelf of unit slope overshoots its gain a little just below its corner.
        Assert.InRange(EffectRig.GainDb(eq, 40), 11, 13.5);
        Assert.True(Math.Abs(EffectRig.GainDb(eq, 3000)) < 0.5);
    }

    [Fact]
    public void TheHighShelfLiftsTheTopAndLeavesTheBottom()
    {
        EqEffect eq = Flat() with { HighGain = -12, HighFreq = 4000 };

        Assert.InRange(EffectRig.GainDb(eq, 15000), -12.5, -9.5);
        Assert.True(Math.Abs(EffectRig.GainDb(eq, 200)) < 0.5);
    }

    [Fact]
    public void AFrequencyPastTheHalfRateIsHeldBelowItInsteadOfBlowingUp()
    {
        RenderedAudio audio = EffectRig.Run(Flat() with { LpFreq = 20000, HpFreq = 1000 }, EffectRig.Probe(1000, 8000));

        Assert.True(Signals.Peak(audio.Left) < 1);
        Assert.True(float.IsFinite(audio.Left[^1]));
    }
}
