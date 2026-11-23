using AudioLab.Audio.Tests.Support;
using AudioLab.Core.Model;

namespace AudioLab.Audio.Tests;

public sealed class LimiterTests
{
    private const double Ceiling = 0.89125093813374556; // -1 dBFS

    private static RenderedAudio Render(double level, bool limiter)
    {
        ProjectDocument document = TestProject.Create(TestProject.Track("t", TrackKind.Audio, TestProject.Clip("c", "m", 0.1, 0.5)))
            with
        { MasterLimiter = limiter };
        MemoryMedia media = new MemoryMedia().Add("m", Signals.Sine(440, 0.5, Rendering.Rate, level));

        return Rendering.Render(document, media);
    }

    [Fact]
    public void BelowTheCeilingTheLimiterChangesNothingAndDelaysNothing()
    {
        RenderedAudio off = Render(0.8, limiter: false);
        RenderedAudio on = Render(0.8, limiter: true);

        Assert.Equal(off.Left, on.Left);
        Assert.Equal(off.Right, on.Right);
        Assert.Equal(0f, on.Left[(int)(0.1 * Rendering.Rate) - 1]);
        Assert.NotEqual(0f, on.Left[(int)(0.1 * Rendering.Rate) + 5]);
    }

    [Fact]
    public void NoSampleExceedsTheCeiling()
    {
        RenderedAudio loud = Render(2, limiter: true);

        Assert.True(Signals.Peak(loud.Left) <= Ceiling + 1e-6);
        Assert.True(Signals.Peak(loud.Left) > Ceiling - 0.01);
    }

    [Fact]
    public void ALoudRightChannelAloneIsLimitedAndTheGainReturnsToUnityAfterwards()
    {
        float[] silent = new float[Rendering.Rate * 4];
        float[] right = new float[silent.Length];
        for (int i = 0; i < Rendering.Rate / 2; i++)
        {
            right[i] = (float)(3 * Math.Sin(i * 0.3));
        }
        for (int i = Rendering.Rate * 2; i < right.Length; i++)
        {
            right[i] = 0.1f;
        }
        ProjectDocument document = TestProject.Create(TestProject.Track("t", TrackKind.Audio, TestProject.Clip("c", "m", 0, 4))) with
        {
            MasterLimiter = true,
        };

        RenderedAudio audio = Rendering.Render(document, new MemoryMedia().Add("m", Signals.Stereo(silent, right, Rendering.Rate)));

        Assert.True(Signals.Peak(audio.Right.AsSpan(0, Rendering.Rate / 2)) <= Ceiling + 1e-6);
        // The stereo pan law at the centre leaves the left silent and the right as it was; long after the burst the gain is back at one.
        Assert.True(Signals.Peak(audio.Left) < 1e-6);
        Assert.Equal(0.1f, audio.Right[(Rendering.Rate * 4) - 10], 5);
    }
}
