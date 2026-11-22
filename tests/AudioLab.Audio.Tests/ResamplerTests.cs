using AudioLab.Audio.Tests.Support;
using AudioLab.Core.Model;

namespace AudioLab.Audio.Tests;

public sealed class ResamplerTests
{
    private static RenderedAudio Render(PcmAudio audio, int rate)
    {
        ProjectDocument document = TestProject.Create(TestProject.Track("t", TrackKind.Audio, TestProject.Clip("c", "m", 0, 1)));

        return Rendering.Render(document, new MemoryMedia().Add("m", audio), rate);
    }

    [Fact]
    public void ASineBroughtUpToAHigherRateKeepsItsFrequencyAndAmplitude()
    {
        RenderedAudio audio = Render(Signals.Sine(1000, 1, 44100), 48000);
        double worst = 0;
        for (int n = 4800; n < 48000 - 4800; n++)
        {
            worst = Math.Max(worst, Math.Abs(audio.Left[n] - (0.5 * Math.Sin(2 * Math.PI * 1000 * n / 48000))));
        }

        Assert.True(worst < 1e-3, $"worst error {worst}");
    }

    [Fact]
    public void ASineBroughtDownKeepsItsAmplitudeAndATonePastTheNewNyquistIsRemoved()
    {
        RenderedAudio kept = Render(Signals.Sine(5000, 1, 96000), 48000);
        RenderedAudio removed = Render(Signals.Sine(40000, 1, 96000), 48000);

        Assert.True(Math.Abs(Signals.Level(kept.Left.AsSpan(4800, 38400), 5000) - 0.5) < 0.01);
        Assert.True(Signals.Peak(removed.Left.AsSpan(4800, 38400)) < 0.5 * 0.01);
    }

    [Fact]
    public void AFileAtTheProjectRateIsNotChanged()
    {
        PcmAudio audio = Signals.Sine(440, 0.1, 48000);

        Assert.Same(audio, Resampler.ToRate(audio, 48000));
    }

    [Fact]
    public void ADirectCurrentLevelSurvivesAnyRatio()
    {
        RenderedAudio audio = Render(Signals.Constant(0.5f, 1, 44100), 48000);

        Assert.Equal(0.5f, audio.Left[24000], 5);
    }
}
