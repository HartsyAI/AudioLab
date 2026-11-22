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
    public void TheHarmonicsThatFoldBackAreFarBelowTheFundamentalAndBelowThoseOfAPlainShaper()
    {
        // 11 kHz at 48 kHz: its third harmonic, 33 kHz, folds down to 15 kHz.
        float[] input = Signals.Sine(11000, 1, 48000, 0.9).Channel(0).ToArray();
        float[] plain = input.Select(x => (float)(Math.Tanh(10 * x) / Math.Tanh(10))).ToArray();

        RenderedAudio audio = EffectRig.Run(Effect(drive: 1), new PcmAudio(48000, [input]));
        double fundamental = Signals.Level(audio.Left.AsSpan(24000), 11000);
        double folded = Signals.Level(audio.Left.AsSpan(24000), 15000);
        double plainFolded = Signals.Level(plain.AsSpan(24000), 15000) / Signals.Level(plain.AsSpan(24000), 11000);

        Assert.True(folded / fundamental < 0.003, $"folded {folded / fundamental} of the fundamental");
        Assert.True(folded / fundamental < 0.1 * plainFolded);
    }

    [Fact]
    public void ForSmallSignalsTheResponseIsFlatToThreeQuartersOfNyquistAndTheDelayIsTheReportedLatency()
    {
        // At drive 0 and a small level the curve is a straight line of slope 1 / tanh(1), so only the filters shape the sound.
        double slope = 1 / Math.Tanh(1);
        foreach (double hertz in new[] { 1000.0, 5000, 10000, 15000, 18000 })
        {
            RenderedAudio audio = EffectRig.Run(Effect(), Signals.Sine(hertz, 1, 48000, 0.01));
            double gain = Signals.Level(audio.Left.AsSpan(24000), hertz) / 0.01 / slope;

            Assert.InRange(20 * Math.Log10(gain), -0.5, 0.5);
        }
        RenderedAudio impulse = EffectRig.Run(Effect(), EffectRig.Impulses(48000, 0.1, 0.01), tail: true);
        int peak = Enumerable.Range(0, impulse.Frames).MaxBy(i => Math.Abs(impulse.Left[i]));

        Assert.Equal(480 + SaturationInsert.Latency, peak);
    }

    [Fact]
    public void WithTheMixHalfwayTheDryAndTheWetBlendWithoutAComb()
    {
        // The wet signal of a small tone is the dry one made 1 / tanh(1) times as large, so the blend must stay flat at every frequency.
        double blend = 0.5 + (0.5 / Math.Tanh(1));
        foreach (double hertz in new[] { 1000.0, 10000, 17000 })
        {
            RenderedAudio audio = EffectRig.Run(Effect(mix: 0.5), Signals.Sine(hertz, 1, 48000, 0.01));
            double gain = Signals.Level(audio.Left.AsSpan(24000), hertz) / 0.01 / blend;

            Assert.InRange(20 * Math.Log10(gain), -0.5, 0.5);
        }
    }

    [Fact]
    public void TheEffectRingsForItsLatencyAfterItsInput()
    {
        RenderPlan plan = RenderPlan.Create(
            TestProject.Create(TestProject.Track("t", TrackKind.Audio, TestProject.Clip("c", "m", 0, 1)) with { Effects = [Effect()] }),
            new MemoryMedia().Add("m", Signals.Constant(0, 1, 48000)),
            new PlanOptions { SampleRate = 48000 });

        Assert.Equal(SaturationInsert.Latency, plan.TailFrames);
    }
}
