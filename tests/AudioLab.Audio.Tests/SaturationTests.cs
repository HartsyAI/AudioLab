using AudioLab.Audio.Tests.Support;
using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Audio.Tests;

public sealed class SaturationTests
{
    private static SaturationEffect Effect(double drive = 0, double trim = 0, double mix = 1) =>
        ((SaturationEffect)EffectRules.Create(EffectKind.Saturation, EffectId.From("s"))) with { Drive = drive, Trim = trim, Mix = mix };

    private static float Settled(SaturationEffect effect, float level) =>
        EffectRig.Run(effect, Signals.Constant(level, 0.1, 8000)).Left[^1];

    [Fact]
    public void ALevelGoesThroughTheTanhCurveOfItsDriveAndIsNormalisedToUnityAtFullScale()
    {
        Assert.Equal(Math.Tanh(0.2) / Math.Tanh(1), Settled(Effect(), 0.2f), 5);
        Assert.Equal(Math.Tanh(10 * 0.2) / Math.Tanh(10), Settled(Effect(drive: 1), 0.2f), 5);
        Assert.Equal(1f, Settled(Effect(drive: 0.5), 1f), 5);
    }

    [Fact]
    public void AnInputBeyondFullScaleIsHeldAtIt()
    {
        Assert.Equal(1f, Settled(Effect(drive: 0.5), 3f), 5);
        Assert.Equal(-1f, Settled(Effect(drive: 0.5), -3f), 5);
    }

    [Fact]
    public void TheTrimScalesTheWetSignalAndTheMixBlendsItWithTheDry()
    {
        float wet = (float)(Math.Tanh(0.2) / Math.Tanh(1));

        Assert.Equal(wet * 0.5f, Settled(Effect(trim: -6.0206), 0.2f), 5);
        Assert.Equal((0.2f * 0.7f) + (wet * 0.3f), Settled(Effect(mix: 0.3), 0.2f), 5);
        Assert.Equal(0.2f, Settled(Effect(mix: 0), 0.2f), 6);
    }

    [Fact]
    public void ASineGainsOnlyOddHarmonics()
    {
        RenderedAudio audio = EffectRig.Run(Effect(drive: 1), Signals.Sine(1000, 1, 48000, 0.5));
        ReadOnlySpan<float> settled = audio.Left.AsSpan(24000);
        double fundamental = Signals.Level(settled, 1000);

        Assert.True(Signals.Level(settled, 3000) > 0.05 * fundamental);
        Assert.True(Signals.Level(settled, 2000) < 0.002 * fundamental);
    }

    [Fact]
    public void TheAntiAliasingKeepsFoldedHarmonicsBelowThoseOfAPlainShaper()
    {
        // 11 kHz at 48 kHz: its third harmonic, 33 kHz, folds down to 15 kHz.
        float[] input = Signals.Sine(11000, 1, 48000, 0.9).Channel(0).ToArray();
        double k = 10;
        float[] plain = input.Select(x => (float)(Math.Tanh(k * x) / Math.Tanh(k))).ToArray();

        RenderedAudio audio = EffectRig.Run(Effect(drive: 1), new PcmAudio(48000, [input]));
        double antialiased = Signals.Level(audio.Left.AsSpan(24000), 15000);
        double naive = Signals.Level(plain.AsSpan(24000), 15000);

        Assert.True(antialiased < naive * 0.5, $"{antialiased} against {naive}");
    }
}
