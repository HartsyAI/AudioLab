using AudioLab.Audio.Tests.Support;
using AudioLab.Core.Model;

namespace AudioLab.Audio.Tests;

public sealed class LayeringRenderTests
{
    private const int Rate = 48000;

    private static readonly MemoryMedia _media = new MemoryMedia()
        .Add("low", Signals.Sine(1000, 2, Rate, 0.4))
        .Add("high", Signals.Sine(3000, 2, Rate, 0.4));

    private static RenderedAudio Render(AudioClip over) =>
        Rendering.Render(
            TestProject.Create(TestProject.Track("t", TrackKind.Audio, TestProject.Clip("under", "low", 0, 1), over)),
            _media,
            Rate);

    [Fact]
    public void WhereClipsOverlapOnlyTheLaterOnePlays()
    {
        RenderedAudio audio = Render(TestProject.Clip("over", "high", 0.4, 0.2));
        ReadOnlySpan<float> covered = audio.Left.AsSpan((int)(0.45 * Rate), (int)(0.1 * Rate));
        ReadOnlySpan<float> before = audio.Left.AsSpan((int)(0.1 * Rate), (int)(0.2 * Rate));

        Assert.True(Signals.Level(covered, 3000, Rate) > 0.35);
        Assert.True(Signals.Level(covered, 1000, Rate) < 0.01);
        Assert.True(Signals.Level(before, 1000, Rate) > 0.35);
    }

    [Fact]
    public void AMutedClipOnTopLetsTheOneBelowThrough()
    {
        RenderedAudio audio = Render(TestProject.Clip("over", "high", 0.4, 0.2) with { Muted = true });

        Assert.True(Signals.Level(audio.Left.AsSpan((int)(0.45 * Rate), (int)(0.1 * Rate)), 1000, Rate) > 0.35);
    }

    [Fact]
    public void TheEdgeOfACoveredClipRampsSoTheSeamDoesNotClick()
    {
        RenderedAudio audio = Render(TestProject.Clip("over", "high", 0.4, 0.2));
        int seam = (int)(0.4 * Rate);
        int ramp = (int)(LayerRulesSeam * Rate);

        Assert.True(Signals.Peak(audio.Left.AsSpan(seam - 1, 1)) < 0.4 * 0.1);
        Assert.True(Signals.Peak(audio.Left.AsSpan(seam - (ramp / 4), ramp / 4)) < 0.4 * 0.3);
    }

    private static double LayerRulesSeam => Core.Rules.LayerRules.SeamFade;
}
