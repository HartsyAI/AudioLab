using AudioLab.Audio.Tests.Support;
using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Audio.Tests;

public sealed class CompressorTests
{
    private static CompressorEffect Effect(double threshold = -30, double ratio = 4, double knee = 0, double makeup = 0) =>
        ((CompressorEffect)EffectRules.Create(EffectKind.Compressor, EffectId.From("c")))
        with
        { Threshold = threshold, Ratio = ratio, Knee = knee, Makeup = makeup, Attack = 0.001, Release = 0.05 };

    private static float Settled(CompressorEffect effect, float level)
    {
        RenderedAudio audio = EffectRig.Run(effect, Signals.Constant(level, 0.5, 8000));

        return audio.Left[^1];
    }

    [Fact]
    public void AboveTheThresholdTheLevelRisesOneDecibelInRatioForEachDecibelIn()
    {
        // 0.5 is 23.98 dB over the threshold, and a ratio of 4 keeps a quarter of that.
        double reduction = -0.75 * ((20 * Math.Log10(0.5)) + 30);

        Assert.Equal(0.5 * Math.Pow(10, reduction / 20), Settled(Effect(), 0.5f), 4);
    }

    [Fact]
    public void BelowTheThresholdNothingChanges()
    {
        Assert.Equal(0.01f, Settled(Effect(), 0.01f), 5);
    }

    [Fact]
    public void ASoftKneeStartsToActAHalfKneeBeforeTheThreshold()
    {
        // At the threshold itself a 20 dB knee takes 1.875 dB off.
        float level = (float)Math.Pow(10, -30.0 / 20);

        Assert.Equal(level * Math.Pow(10, -1.875 / 20), Settled(Effect(knee: 20), level), 5);
        Assert.Equal(level * 0.2f, Settled(Effect(knee: 20), level * 0.2f), 6);
    }

    [Fact]
    public void TheMakeupAddsItsGainAfterTheCompression()
    {
        Assert.Equal(Settled(Effect(), 0.5f) * 2, Settled(Effect(makeup: 6.0206), 0.5f), 4);
    }

    [Fact]
    public void ARatioOfOneLeavesTheSignalAlone()
    {
        Assert.Equal(0.5f, Settled(Effect(ratio: 1), 0.5f), 5);
    }

    [Fact]
    public void ASlowAttackLetsTheStartThroughAndAFastOneCatchesIt()
    {
        CompressorEffect fast = Effect() with { Attack = 0.001 };
        CompressorEffect slow = Effect() with { Attack = 0.5 };
        PcmAudio step = Signals.Constant(0.5f, 0.3, 8000);

        float fastLevel = EffectRig.Run(fast, step).Left[400];
        float slowLevel = EffectRig.Run(slow, step).Left[400];

        Assert.True(fastLevel < 0.1);
        Assert.True(slowLevel > 0.3);
    }

    [Fact]
    public void BothChannelsAreCompressedByTheLouderOneSoTheImageHolds()
    {
        PcmAudio loudLeft = Signals.Stereo(Enumerable.Repeat(0.5f, 4000).ToArray(), Enumerable.Repeat(0.05f, 4000).ToArray(), 8000);

        RenderedAudio audio = EffectRig.Run(Effect(), loudLeft);

        Assert.Equal(audio.Left[^1] / 0.5, audio.Right[^1] / 0.05, 3);
    }

    [Fact]
    public void AHardKneeAtExactlyTheThresholdDoesNotDivideByZero()
    {
        // Full scale against a threshold of 0 dB with no knee puts the level exactly on the threshold.
        float settled = Settled(Effect(threshold: 0, knee: 0), 1f);

        Assert.True(float.IsFinite(settled));
        Assert.Equal(1f, settled, 5);
    }
}
