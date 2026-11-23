using AudioLab.Audio.Instruments;
using AudioLab.Audio.Tests.Support;
using AudioLab.Core.Model;
using static AudioLab.Audio.Tests.Support.SynthProject;

namespace AudioLab.Audio.Tests;

public sealed class SamplerTests
{
    private const int Rate = Rendering.Rate;
    private const string SampleId = "smp";

    private static SamplerInstrument Sampler(int root = 69) => InstrumentPresets.CreateSampler() with
    {
        Sample = MediaId.From(SampleId),
        SampleName = "sample",
        RootNote = root,
        Attack = 0.001,
        Release = 0.05,
        Gain = 1,
        OneShot = false,
    };

    private static MemoryMedia Media(PcmAudio audio) => new MemoryMedia().Add(SampleId, audio);

    private static PcmAudio Tone(double hertz, double seconds, int channels = 2) => Signals.Sine(hertz, seconds, Rate, 0.5, channels);

    private static ProjectDocument Notes(SamplerInstrument sampler, params MidiNote[] notes) =>
        Project(InstrumentTrack("t", sampler, Clip("c", 0, 4, notes)));

    private static RenderedAudio Play(SamplerInstrument sampler, PcmAudio audio, params MidiNote[] notes) =>
        Render(Notes(sampler, notes), Rate, tail: true, media: Media(audio));

    private static PolyphonicSource Source(SamplerInstrument sampler, PcmAudio audio, params MidiNote[] notes)
    {
        ProjectDocument project = Notes(sampler, notes);
        return (PolyphonicSource)new InstrumentSourceFactory().Create(project, project.Tracks[0], new AudioFormat(Rate, 2), new Plan(audio))!;
    }

    [Theory]
    [InlineData(69, 440)]
    [InlineData(81, 880)]
    [InlineData(57, 220)]
    [InlineData(72, 523.25)]
    public void ANoteSoundsAtTheSamplesPitchMovedBySemitonesFromTheRootNote(int pitch, double expected)
    {
        RenderedAudio audio = Play(Sampler(), Tone(440, 1), Note(1, pitch, 0, 0.6));

        Assert.Equal(expected, Frequency(audio, 0.1, 0.5), 0);
    }

    [Fact]
    public void ANoteAtTheRootNoteGivesTheSampleItselfTimesGainAndVelocity()
    {
        Random random = new(2);
        float[] left = [.. Enumerable.Range(0, 4000).Select(_ => (float)((random.NextDouble() * 2) - 1))];
        float[] right = [.. Enumerable.Range(0, 4000).Select(_ => (float)((random.NextDouble() * 2) - 1))];

        RenderedAudio audio = Play(Sampler() with { Gain = 0.5 }, Signals.Stereo(left, right, Rate), Note(1, 69, 0, 1, 64));

        float level = 0.5f * (64 / 127f);
        for (int i = 100; i < 3900; i++)
        {
            Assert.Equal(left[i] * level, audio.Left[i], 5);
            Assert.Equal(right[i] * level, audio.Right[i], 5);
        }
    }

    [Fact]
    public void APitchedNoteFollowsTheSineItWasMadeFromWithinAThousandth()
    {
        RenderedAudio audio = Play(Sampler(), Tone(440, 1, channels: 2), Note(1, 76, 0, 0.5));

        double ratio = Math.Pow(2, 7 / 12.0);
        double worst = 0;
        for (int i = 400; i < 3000; i++)
        {
            double expected = 0.5 * Math.Sin(2 * Math.PI * 440 * ratio * i / Rate);
            worst = Math.Max(worst, Math.Abs(audio.Left[i] - expected));
        }
        Assert.True(worst < 0.001, $"Worst error {worst}.");
    }

    [Fact]
    public void TheAttackRisesLinearlyAndTheReleaseFallsFromTheLevelReached()
    {
        PcmAudio constant = Signals.Constant(1, 3, Rate, 2);

        RenderedAudio audio = Play(Sampler() with { Attack = 0.2, Release = 0.4 }, constant, Note(1, 69, 0, 1));

        Assert.Equal(0.5, Rendering.At(audio, 0.1), 0.02);
        Assert.Equal(1.0, Rendering.At(audio, 0.5), 0.001);
        Assert.Equal(Math.Exp(-1), Rendering.At(audio, 1.1), 0.02);
    }

    [Fact]
    public void ANoteShorterThanTheAttackIsHeardAndItsReleaseGoesOnFromTheLevelItHadReached()
    {
        RenderedAudio audio = Play(Sampler() with { Attack = 0.5, Release = 0.2 }, Signals.Constant(1, 3, Rate, 2), Note(1, 69, 0, 0.1));

        Assert.Equal(0.2, Rendering.At(audio, 0.099), 0.01);
        float biggestStep = 0;
        for (int i = 1; i < audio.Frames; i++)
        {
            biggestStep = Math.Max(biggestStep, Math.Abs(audio.Left[i] - audio.Left[i - 1]));
        }
        Assert.True(biggestStep < 0.01, "The release jumped.");
    }

    [Fact]
    public void AHeldNoteStopsAtItsEndWhereAOneShotPlaysTheWholeSample()
    {
        PcmAudio constant = Signals.Constant(1, 2, Rate, 2);

        RenderedAudio gated = Play(Sampler(), constant, Note(1, 69, 0, 0.3));
        RenderedAudio oneShot = Play(Sampler() with { OneShot = true }, constant, Note(1, 69, 0, 0.3));

        Assert.Equal(0, Rendering.At(gated, 1.0), 1e-6);
        Assert.Equal(1.0, Rendering.At(oneShot, 1.5), 1e-5);
        Assert.Equal(0, Rendering.At(oneShot, 2.05), 1e-6);
        Assert.True(oneShot.Frames >= 2 * Rate, "The tail did not cover the sample.");
    }

    // Audit row 50: a note held past its sample's end kept its gain node.
    [Fact]
    public void ANoteHeldPastTheEndOfItsSampleEndsThereAndFreesItsVoice()
    {
        PolyphonicSource source = Source(Sampler(), Signals.Constant(1, 0.5, Rate, 2), Note(1, 69, 0, 3));
        float[] left = new float[Rate];
        float[] right = new float[Rate];

        source.Render(0, left, right);

        Assert.Equal(1, left[(int)(0.4 * Rate)], 3);
        Assert.Equal(0, left[(int)(0.6 * Rate)]);
        Assert.Equal(0, source.SoundingVoices);
    }

    // Audit row 34: stopping with an empty sampler threw.
    [Fact]
    public void ASamplerWithNoSampleIsSilentAndRendersWithoutAnError()
    {
        SamplerInstrument empty = InstrumentPresets.CreateSampler();
        ProjectDocument project = Notes(empty, Note(1, 60, 0, 1));
        Assert.Null(new InstrumentSourceFactory().Create(project, project.Tracks[0], new AudioFormat(Rate, 2), new Plan(null)));

        RenderedAudio audio = Render(project, Rate, tail: true, media: new MemoryMedia());

        Assert.Equal(0, Peak(audio, 0, 4));
    }

    [Fact]
    public void ASampleThatIsNotThereFailsThePlanLikeAMissingClipExceptWhereNothingWouldSound()
    {
        ProjectDocument heard = Notes(Sampler(), Note(1, 60, 0, 1));

        MissingMediaException exception = Assert.Throws<MissingMediaException>(() => Render(heard, Rate, media: new MemoryMedia()));
        Assert.Equal([MediaId.From(SampleId)], exception.Missing);
        Assert.Throws<MissingMediaException>(() => PlanFor(heard));

        Track track = heard.Tracks[0];
        Assert.Equal(0, Peak(Render(heard with { Tracks = [track with { Muted = true }] }, Rate, media: new MemoryMedia()), 0, 1));
        MidiClip muted = track.MidiClips[0] with { Muted = true };
        Render(heard with { Tracks = [track with { MidiClips = [muted] }] }, Rate, media: new MemoryMedia());
        MidiClip noNotes = track.MidiClips[0] with { Notes = [] };
        Render(heard with { Tracks = [track with { MidiClips = [noNotes] }] }, Rate, media: new MemoryMedia());
    }

    [Fact]
    public void AMonoSampleIsTheSameOnBothChannelsAndAStereoOneKeepsItsChannels()
    {
        PcmAudio mono = Signals.Constant(0.5f, 1, Rate, 1);
        PcmAudio stereo = Signals.Stereo(Enumerable.Repeat(0.5f, Rate).ToArray(), new float[Rate], Rate);

        RenderedAudio fromMono = Play(Sampler(), mono, Note(1, 69, 0, 0.5));
        RenderedAudio fromStereo = Play(Sampler(), stereo, Note(1, 69, 0, 0.5));

        Assert.Equal(fromMono.Left, fromMono.Right);
        Assert.True(Peak(fromMono, 0.1, 0.4) > 0.3);
        Assert.True(Peak(fromStereo, 0.1, 0.4) > 0.4);
        Assert.Equal(0, fromStereo.Right.Max(Math.Abs));
    }

    [Fact]
    public void ASeekIntoANoteContinuesTheSampleWhereItWouldBe()
    {
        float[] ramp = [.. Enumerable.Range(0, 10 * Rate).Select(i => i / (10f * Rate))];
        ProjectDocument project = Notes(Sampler(), Note(1, 69, 0, 8));
        RenderPlan plan = RenderPlan.Create(
            project,
            Media(Signals.Stereo(ramp, ramp, Rate)),
            new PlanOptions { SampleRate = Rate, Sources = new InstrumentSourceFactory() });
        MixerGraph graph = new(plan);
        float[] left = new float[Rate / 2];
        float[] right = new float[Rate / 2];

        graph.Seek(3 * Rate);
        graph.Render(left, right);

        for (int k = 100; k < left.Length; k += 37)
        {
            Assert.Equal((3 * Rate) + k, left[k] * 10f * Rate, 0.5);
        }
    }

    [Fact]
    public void BlockSizeNeverChangesTheSoundAndTwoRendersAreEqual()
    {
        MidiNote[] notes = [.. Enumerable.Range(0, 40).Select(i => Note(i + 1, 40 + ((i * 7) % 50), i * 0.07, 0.3 + (i % 5 * 0.2), 30 + (i * 2)))];
        ProjectDocument project = Notes(Sampler(60) with { Release = 0.3 }, notes);
        MemoryMedia media = Media(Tone(300, 2));
        RenderedAudio reference = Render(project, Rate, 512, media: media);

        foreach (int block in new[] { 1, 7, 64, 480, 4096 })
        {
            Assert.Equal(reference.Left, Render(project, Rate, block, media: media).Left);
        }
        Assert.Equal(reference.Left, Render(project, Rate, 512, media: media).Left);
    }

    [Fact]
    public void ARenderThatStartsNotesEndsThemAndTakesOverVoicesAllocatesNothing()
    {
        MidiNote[] notes = [.. Enumerable.Range(0, 1500).Select(i => Note(i + 1, 30 + (i % 60), i * 0.04, 0.4 + ((i % 9) * 0.3), 40 + (i % 80)))];
        RenderPlan plan = PlanFor(Notes(Sampler(60) with { Release = 0.5 }, notes), Tone(220, 1.5));
        MixerGraph graph = new(plan);
        float[] left = new float[480];
        float[] right = new float[480];

        Assert.True(AllocationProbe.SettlesAtZero(() => graph.Render(left, right)));
    }

    [Fact]
    public void RandomSettingsAndPitchesStayFiniteAndInsideTheSamplesRange()
    {
        Random random = new(8);
        PcmAudio noise = Noise.Create(Rate, 1.5, 1);
        for (int round = 0; round < 80; round++)
        {
            SamplerInstrument sampler = Sampler(random.Next(0, 128)) with
            {
                Attack = 0.001 + (random.NextDouble() * 0.5),
                Release = 0.01 + (random.NextDouble() * 2),
                Gain = random.NextDouble(),
                OneShot = random.Next(2) == 0,
            };
            MidiNote[] notes = [.. Enumerable.Range(0, 30).Select(i =>
                Note(i + 1, random.Next(0, 128), random.NextDouble() * 2, 0.01 + random.NextDouble(), random.Next(1, 128)))];
            PolyphonicSource source = Source(sampler, noise, notes);
            float[] left = new float[6 * Rate];
            float[] right = new float[left.Length];

            for (int done = 0; done < left.Length; done += 997)
            {
                int count = Math.Min(997, left.Length - done);
                source.Render(done, left.AsSpan(done, count), right.AsSpan(done, count));
            }

            Assert.True(left.All(float.IsFinite) && right.All(float.IsFinite), $"round {round}");
            Assert.True(left.Max(Math.Abs) <= 30 * 1.0001f * 0.5f + 1, $"round {round}");
        }
    }

    [Fact]
    public void ASampleSoundsNoLongerThanItsOwnLengthAtThePitchOfTheNote()
    {
        PcmAudio second = Signals.Constant(1, 1, Rate, 2);

        ProjectDocument project = Project(InstrumentTrack("t", Sampler(60) with { OneShot = true }, Clip("c", 0, 1, [Note(1, 36, 0, 0.2)])));

        RenderedAudio low = Render(project, Rate, media: Media(second));

        Assert.InRange(low.Frames / (double)Rate, 3.99, 4.2);
        Assert.Equal(1.0, Rendering.At(low, 3.9), 1e-4);
    }

    [Fact]
    public void AOneShotThatWouldPlayPastThirtySecondsAfterItsNoteFadesOutAtThatLimit()
    {
        PcmAudio long40 = Signals.Constant(1, 40, Rate, 2);
        ProjectDocument project = Project(InstrumentTrack("t", Sampler() with { OneShot = true }, Clip("c", 0, 1, [Note(1, 69, 0, 0.5)])));

        RenderedAudio audio = Render(project, Rate, media: Media(long40));

        Assert.InRange(audio.Frames / (double)Rate, 30.4, 30.6);
        Assert.Equal(1.0, Rendering.At(audio, 30.0), 1e-4);
        Assert.True(Peak(audio, 30.499, 30.5) < 0.1, "The cut was not faded.");
        Assert.True(Peak(audio, 30.46, 30.5) < 1);
    }

    [Fact]
    public void ThirtyNotesAtOnceTakeVoicesWithoutAClickOrAnError()
    {
        MidiNote[] notes = [.. Enumerable.Range(0, 40).Select(i => Note(i + 1, 50 + (i % 20), i * 0.01, 3))];
        PolyphonicSource source = Source(Sampler(60), Signals.Constant(0.01f, 6, Rate, 2), notes);
        float[] left = new float[Rate / 2];
        float[] right = new float[Rate / 2];

        source.Render(0, left, right);

        Assert.True(source.CountedVoices <= PolyphonicSource.MaxHeldVoices);
        Assert.True(left.All(float.IsFinite));
    }

    private static RenderPlan PlanFor(ProjectDocument project, PcmAudio? audio = null) =>
        RenderPlan.Create(
            project,
            audio is null ? new MemoryMedia() : Media(audio),
            new PlanOptions { SampleRate = Rate, Sources = new InstrumentSourceFactory() });

    private sealed class Plan(PcmAudio? audio) : IPlanMedia
    {
        public PcmAudio? Get(MediaId id) => audio;
    }
}
