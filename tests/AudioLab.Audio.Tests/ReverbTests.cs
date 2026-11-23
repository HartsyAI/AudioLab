using AudioLab.Audio.Tests.Support;
using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Audio.Tests;

public sealed class ReverbTests
{
    private const int Rate = 8000;

    private static ReverbEffect Effect() =>
        ((ReverbEffect)EffectRules.Create(EffectKind.Reverb, EffectId.From("r"))) with { Size = 0.5, Decay = 2, Predelay = 0.01, Mix = 1 };

    [Fact]
    public void TheImpulseIsTheOldDawsSeededNoiseWithAPowerLawDecay()
    {
        // Values from the old generator (mulberry32 seeded from the size, the decay and the channel), run in JavaScript.
        float[] left = ReverbImpulse.Create(0.5, 2, 0, Rate);
        float[] right = ReverbImpulse.Create(0.5, 2, 1, Rate);

        Assert.Equal(4000, left.Length);
        Assert.Equal([-0.629909992f, -0.497037649f, -0.988195598f, -0.0900697783f], [left[0], left[1], left[2], left[1000]]);
        Assert.Equal([-0.139786452f, 0.0957058519f, -0.649470389f, 0.0371228345f], [right[0], right[1], right[2], right[1000]]);
        Assert.True(Math.Abs(left[3999]) < 1e-6);
    }

    [Fact]
    public void ARoomIsTheSameEveryTimeAndDiffersWithItsSettings()
    {
        Assert.Equal(ReverbImpulse.Create(0.5, 2, 0, Rate), ReverbImpulse.Create(0.5, 2, 0, Rate));
        Assert.NotEqual(ReverbImpulse.Create(0.5, 2, 0, Rate), ReverbImpulse.Create(0.5, 2.01, 0, Rate));
        Assert.NotEqual(ReverbImpulse.Create(0.5, 2, 0, Rate), ReverbImpulse.Create(0.5, 2, 1, Rate));
    }

    [Fact]
    public void AnImpulseComesBackAsTheScaledImpulseResponseAfterThePredelay()
    {
        RenderedAudio audio = EffectRig.Run(Effect(), EffectRig.Impulses(Rate, 1, 0), tail: false);
        float[] left = ReverbImpulse.Create(0.5, 2, 0, Rate);
        float[] right = ReverbImpulse.Create(0.5, 2, 1, Rate);
        float scale = (float)ReverbImpulse.Scale(left, right, Rate);
        int predelay = 80;

        Assert.Equal(0, Signals.Peak(audio.Left.AsSpan(0, predelay)));
        for (int k = 0; k < 4000; k += 37)
        {
            Assert.Equal(left[k] * scale, audio.Left[predelay + k], 4);
            Assert.Equal(right[k] * scale, audio.Right[predelay + k], 4);
        }
    }

    [Fact]
    public void AMonoSourceComesOutDecorrelatedBecauseEachChannelHasItsOwnRoom()
    {
        RenderedAudio audio = EffectRig.Run(Effect(), EffectRig.Impulses(Rate, 1, 0));

        Assert.NotEqual(audio.Left[200], audio.Right[200]);
    }

    [Fact]
    public void WithNoMixTheSignalIsUntouched()
    {
        PcmAudio input = Signals.Sine(300, 0.5, Rate);

        RenderedAudio audio = EffectRig.Run(Effect() with { Mix = 0 }, input);

        Assert.Equal(input.Channel(0).ToArray(), audio.Left);
    }

    [Fact]
    public void TheTailIsThePredelayPlusTheRoom()
    {
        ProjectDocument document = TestProject.Create(
            TestProject.Track("t", TrackKind.Audio, TestProject.Clip("c", "m", 0, 1)) with { Effects = [Effect()] });

        MemoryMedia media = new MemoryMedia().Add("m", Signals.Constant(0, 1, Rate));

        RenderPlan plan = RenderPlan.Create(document, media, new PlanOptions { SampleRate = Rate });

        Assert.Equal(0.51 * Rate, plan.TailFrames);
    }

    [Fact]
    public void ABusReverbRingsAfterTheTracksThatFeedIt()
    {
        Track track = TestProject.Track("t", TrackKind.Audio, TestProject.Clip("c", "m", 0, 1)) with
        {
            Sends = [new Send(TrackId.From("b"), 1, false, false)],
        };
        Track bus = TestProject.Track("b", TrackKind.Bus) with { Effects = [Effect()] };
        ProjectDocument document = TestProject.Create(track, bus);
        MemoryMedia media = new MemoryMedia().Add("m", EffectRig.Impulses(Rate, 1, 0.99));

        RenderedAudio audio = OfflineRenderer.Render(document, media, new OfflineOptions { SampleRate = Rate, IncludeTail = true });

        Assert.Equal(Rate + (int)(0.51 * Rate), audio.Frames);
        Assert.True(Signals.Peak(audio.Left.AsSpan(Rate + 200, 2000)) > 0);
    }
}
