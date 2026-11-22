using AudioLab.Audio.Tests.Support;
using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Audio.Tests;

public sealed class DelayTests
{
    private const int Rate = 48000;

    private static DelayEffect Effect() =>
        ((DelayEffect)EffectRules.Create(EffectKind.Delay, EffectId.From("d"))) with { Time = 0.05, Feedback = 0.5, Tone = 15000, Mix = 1 };

    // The frame of the largest sample in a window around an expected time.
    private static (int Frame, float Peak) Echo(RenderedAudio audio, double seconds, double width = 0.01)
    {
        int centre = (int)Math.Round(seconds * audio.Format.SampleRate);
        int half = (int)(width * audio.Format.SampleRate);
        int best = centre - half;
        for (int i = centre - half; i <= centre + half; i++)
        {
            if (Math.Abs(audio.Left[i]) > Math.Abs(audio.Left[best]))
            {
                best = i;
            }
        }
        return (best, audio.Left[best]);
    }

    [Fact]
    public void EachEchoComesOneDelayTimeAfterTheLastAndIsQuieterByTheFeedback()
    {
        RenderedAudio audio = EffectRig.Run(Effect(), EffectRig.Impulses(Rate, 1, 0.1), tail: true);

        (int first, float firstPeak) = Echo(audio, 0.15);
        (int second, float secondPeak) = Echo(audio, 0.2);

        Assert.InRange(first, (int)(0.15 * Rate) - 3, (int)(0.15 * Rate) + 3);
        Assert.InRange(second, (int)(0.2 * Rate) - 3, (int)(0.2 * Rate) + 3);
        Assert.InRange(secondPeak / firstPeak, 0.3, 0.55);
        Assert.Equal(0, audio.Left[(int)(0.1 * Rate)], 6);
    }

    [Fact]
    public void TheMixBlendsTheDrySignalWithTheEchoes()
    {
        RenderedAudio audio = EffectRig.Run(Effect() with { Mix = 0.5 }, EffectRig.Impulses(Rate, 1, 0.1));

        Assert.Equal(0.5f, audio.Left[(int)(0.1 * Rate)], 3);
    }

    [Fact]
    public void ASyncedDelayFollowsTheTempoAndRetunesAtATempoChange()
    {
        // A quarter note: 0.5 s at 120 beats a minute and 1 s at 60, from 1 s on.
        DelayEffect synced = Effect() with { Sync = true, Division = "1/4", Feedback = 0, Tone = 15000 };
        TempoChange slower = new(TempoChangeId.From("tc"), 1, 60);

        RenderedAudio audio = EffectRig.Run(synced, EffectRig.Impulses(Rate, 4, 0.1, 2), tail: false, bpm: 120, slower);

        Assert.InRange(Echo(audio, 0.6, 0.02).Frame, (int)(0.6 * Rate) - 3, (int)(0.6 * Rate) + 3);
        Assert.InRange(Echo(audio, 3, 0.02).Frame, (3 * Rate) - 3, (3 * Rate) + 3);
    }

    [Fact]
    public void ADelayRingsUntilItsEchoesAreSixtyDecibelsDown()
    {
        RenderPlan plan = RenderPlan.Create(
            TestProject.Create(
                TestProject.Track("t", TrackKind.Audio, TestProject.Clip("c", "m", 0, 1)) with
                {
                    Effects = [Effect() with { Time = 0.5, Feedback = 0.5 }],
                }),
            new MemoryMedia().Add("m", Signals.Constant(0, 1, Rate)),
            new PlanOptions { SampleRate = Rate });

        // 60 dB is ten halvings of 6.02 dB, so ten repeats after the first echo.
        Assert.Equal(0.5 * (1 + (60 / (20 * Math.Log10(2)))), (double)plan.TailFrames / Rate, 1);
    }

    [Fact]
    public void ADelayWithNoFeedbackRingsForOneTimeOnly()
    {
        RenderPlan plan = RenderPlan.Create(
            TestProject.Create(
                TestProject.Track("t", TrackKind.Audio, TestProject.Clip("c", "m", 0, 1)) with
                {
                    Effects = [Effect() with { Time = 0.5, Feedback = 0 }],
                }),
            new MemoryMedia().Add("m", Signals.Constant(0, 1, Rate)),
            new PlanOptions { SampleRate = Rate });

        Assert.Equal(0.5, (double)plan.TailFrames / Rate, 3);
    }
}
