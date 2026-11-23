using AudioLab.Audio.Live;
using AudioLab.Audio.Tests.Support;
using AudioLab.Core.Model;
using AudioLab.Core.Playback;

namespace AudioLab.Audio.Tests;

public sealed class LiveEngineTests
{
    private const int Rate = 8000;
    private const int Chunk = 128;

    private static RenderPlan PlanOf(PcmAudio audio, double seconds = 2)
    {
        MemoryMedia media = new MemoryMedia().Add("m", audio);
        Track track = TestProject.Track("t", TrackKind.Audio, TestProject.Clip("c", "m", 0, seconds));
        return RenderPlan.Create(TestProject.Create(track), media, new PlanOptions { SampleRate = Rate });
    }

    private static PcmAudio Ramp(int frames)
    {
        float[] ramp = new float[frames];
        for (int i = 0; i < frames; i++)
        {
            ramp[i] = i / (float)frames;
        }
        return Signals.Stereo(ramp, ramp, Rate);
    }

    private static (float[] Left, float[] Right) Pull(LiveEngine engine, int chunks, int size = Chunk)
    {
        float[] left = new float[chunks * size];
        float[] right = new float[chunks * size];
        for (int i = 0; i < chunks; i++)
        {
            engine.Render(left.AsSpan(i * size, size), right.AsSpan(i * size, size));
        }
        return (left, right);
    }

    [Fact]
    public void PausingKeepsThePlayheadAndStoppingReturnsToWherePlayingBegan()
    {
        LiveEngine engine = new(Rate, Chunk);
        engine.Install(PlanOf(Signals.Constant(0.5f, 2, Rate)), 1, null);
        engine.Seek(800);

        engine.Play();
        Pull(engine, 10);
        Assert.Equal(TransportState.Playing, engine.State);
        Assert.Equal(800 + (10 * Chunk), engine.Position);

        engine.Pause();
        Pull(engine, 1);
        long paused = engine.Position;
        (float[] silent, _) = Pull(engine, 2);
        Assert.Equal(TransportState.Paused, engine.State);
        Assert.Equal(paused, engine.Position);
        Assert.All(silent, sample => Assert.Equal(0, sample));

        engine.Play();
        Pull(engine, 3);
        engine.Stop();
        Pull(engine, 1);
        Assert.Equal(TransportState.Stopped, engine.State);
        Assert.Equal(paused, engine.Position);
        Assert.Equal(paused, engine.Cue);
    }

    [Fact]
    public void PauseAndStopTakeEffectAtOnceWhenThereIsNoPlanYetSoAClipThatNeverLoadsCannotHoldTheTransport()
    {
        LiveEngine engine = new(Rate, Chunk);
        engine.Seek(800);

        engine.Play();
        engine.Pause();
        Assert.Equal(TransportState.Paused, engine.State);
        engine.Play();
        engine.Seek(1600);
        engine.Stop();

        Assert.Equal(TransportState.Stopped, engine.State);
        Assert.Equal(800, engine.Position);
        Assert.Equal(800, engine.Cue);
    }

    [Fact]
    public void AJumpWhilePlayingFadesTheChunkItInterruptsOutAndTheNextOneInSoItDoesNotClick()
    {
        LiveEngine engine = new(Rate, 512);
        engine.Install(PlanOf(Signals.Sine(200, 2, Rate, 0.5, 1)), 1, null);
        engine.Play();
        Pull(engine, 10, 512);

        engine.Seek(3333);
        (float[] after, _) = Pull(engine, 4, 512);

        // A sine of 200 Hz at 8 kHz and 0.5 moves at most 0.0785 a sample; a hard jump to another phase would step by up to a whole 1.
        for (int i = 1; i < after.Length; i++)
        {
            Assert.True(Math.Abs(after[i] - after[i - 1]) < 0.12f, $"a step of {after[i] - after[i - 1]} at {i}");
        }
        Assert.Equal(3333 + (3 * 512), engine.Position);
    }

    [Fact]
    public void ALoopWrapThatJumpsToAnotherPhaseIsRampedDownAndUpSoItDoesNotClick()
    {
        LiveEngine engine = new(Rate, 512);
        engine.Install(PlanOf(Signals.Sine(200, 2, Rate, 0.5, 1)), 1, null);
        engine.SetLoop(true, 1000, 3010);
        engine.Seek(2000);
        engine.Play();

        (float[] output, _) = Pull(engine, 8, 512);

        for (int i = 300; i < output.Length; i++)
        {
            Assert.True(Math.Abs(output[i] - output[i - 1]) < 0.12f, $"a step of {output[i] - output[i - 1]} at {i}");
        }
    }

    [Fact]
    public void PlayingFadesInAndAPauseFadesOutSoNeitherClicks()
    {
        LiveEngine engine = new(Rate, 512);
        engine.Install(PlanOf(Signals.Constant(0.5f, 2, Rate)), 1, null);
        engine.Play();

        (float[] start, _) = Pull(engine, 2, 512);
        engine.Pause();
        (float[] end, _) = Pull(engine, 1, 512);

        Assert.True(start[0] < 0.01f);
        Assert.Equal(0.5f, start[300], 3);
        Assert.Equal(0.5f, start[512], 3);
        Assert.Equal(0.5f, end[200], 3);
        Assert.Equal(0, end[^1]);
    }

    [Fact]
    public void PlayingDoesNotStopAtTheEndOfTheContent()
    {
        LiveEngine engine = new(Rate, Chunk);
        engine.Install(PlanOf(Signals.Constant(0.5f, 1, Rate), 1), 1, null);
        engine.Play();

        Pull(engine, 100);

        Assert.Equal(TransportState.Playing, engine.State);
        Assert.Equal(100 * Chunk, engine.Position);
    }

    [Fact]
    public void ALoopWrapsAtItsEndToItsStartAndOnlyTheFramesAroundTheWrapAreRamped()
    {
        PcmAudio ramp = Ramp(2 * Rate);
        LiveEngine engine = new(Rate, 512);
        engine.Install(PlanOf(ramp), 1, null);
        engine.SetLoop(true, 1000, 3000);
        engine.Seek(2300);
        engine.Play();
        Pull(engine, 1, 512);

        (float[] left, _) = Pull(engine, 1, 512);

        // The chunk starts at 2812 and reaches the loop's end at 3000, 188 frames in; 64 frames either side are ramped, the rest is the music.
        for (int i = 0; i < 120; i++)
        {
            Assert.Equal(ramp.Channel(0)[2812 + i], left[i], 5);
        }
        for (int i = 260; i < 512; i++)
        {
            Assert.Equal(ramp.Channel(0)[1000 + (i - 188)], left[i], 5);
        }
        Assert.Equal(1000 + (512 - 188), engine.Position);
    }

    [Fact]
    public void ASwappedPlanIsCrossfadedOverOneChunkAndTakesOverAfterIt()
    {
        LiveEngine engine = new(Rate, Chunk);
        engine.Install(PlanOf(Signals.Constant(0.5f, 2, Rate)), 1, null);
        engine.Play();
        Pull(engine, 4);

        engine.Install(PlanOf(Signals.Constant(0.25f, 2, Rate)), 2, null);
        Assert.Equal(1, engine.Revision);
        (float[] left, _) = Pull(engine, 2);

        Assert.Equal(2, engine.Revision);
        Assert.Equal(0.5f, left[0], 2);
        Assert.Equal(0.25f, left[Chunk - 1], 2);
        for (int i = 1; i < Chunk; i++)
        {
            Assert.True(left[i] <= left[i - 1] + 1e-6f);
        }
        Assert.All(left.AsSpan(Chunk).ToArray(), sample => Assert.Equal(0.25f, sample, 4));
    }

    [Fact]
    public void ASwapToTheSamePlanChangesNothingAudible()
    {
        PcmAudio sine = Signals.Sine(200, 2, Rate, 0.5, 1);
        LiveEngine plain = new(Rate, Chunk);
        plain.Install(PlanOf(sine), 1, null);
        plain.Play();
        LiveEngine swapped = new(Rate, Chunk);
        swapped.Install(PlanOf(sine), 1, null);
        swapped.Play();

        (float[] expected, _) = Pull(plain, 12);
        Pull(swapped, 5);
        swapped.Install(PlanOf(sine), 2, null);
        (float[] after, _) = Pull(swapped, 7);

        for (int i = 0; i < after.Length; i++)
        {
            Assert.Equal(expected[(5 * Chunk) + i], after[i], 5);
        }
    }

    private static float[] ClickRun(long start, int size, int length)
    {
        ProjectDocument empty = TestProject.Create();
        LiveEngine engine = new(Rate, Chunk);
        engine.Install(RenderPlan.Create(empty, new MemoryMedia(), new PlanOptions { SampleRate = Rate }), 1, new ClickGenerator(empty, Rate));
        engine.SetClick(true);
        engine.Seek(start);
        engine.Play();
        engine.Render(new float[size], new float[size]);
        return Pull(engine, length / size, size).Left;
    }

    [Fact]
    public void TheClickSoundsOnTheBeatAndGivesTheSameSoundWhateverTheChunkSize()
    {
        // 120 beats a minute at 8 kHz is a beat every 4000 frames, so the first click after 3900 starts at 4000, which is 28 frames
        // into what the 128-frame run keeps after its first chunk and the start of what the 100-frame run keeps.
        float[] wide = ClickRun(3900, 128, 1280);
        float[] narrow = ClickRun(3900, 100, 1300);

        Assert.True(Signals.Peak(wide.AsSpan(0, 200)) > 0.1);
        Assert.Equal(0, Signals.Peak(wide.AsSpan(300, 100)));
        for (int i = 28; i < wide.Length + 28 && i < narrow.Length; i++)
        {
            Assert.Equal(wide[i - 28], narrow[i], 5);
        }
    }

    [Fact]
    public void TheDownbeatClickIsHigherThanTheOthers()
    {
        float[] beat = ClickRun(3900, 128, 1280).AsSpan(0, 200).ToArray();
        float[] downbeat = ClickRun(15900, 128, 1280).AsSpan(0, 200).ToArray();

        Assert.True(Signals.Level(beat, 1000, Rate) > Signals.Level(beat, 1500, Rate));
        Assert.True(Signals.Level(downbeat, 1500, Rate) > Signals.Level(downbeat, 1000, Rate));
    }

    [Fact]
    public void RenderingAllocatesNothingWhilePlayingLoopingWithTheClick()
    {
        LiveEngine engine = new(Rate, Chunk);
        engine.Install(PlanOf(Signals.Sine(200, 2, Rate, 0.5, 2)), 1, new ClickGenerator(TestProject.Create(), Rate));
        engine.SetLoop(true, 0, 1000);
        engine.SetClick(true);
        engine.Play();
        float[] left = new float[Chunk];
        float[] right = new float[Chunk];

        bool silent = AllocationProbe.SettlesAtZero(() => engine.Render(left, right));

        Assert.True(silent);
    }
}
