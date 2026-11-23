using AudioLab.Audio.Instruments;
using AudioLab.Audio.Tests.Support;
using AudioLab.Core.Model;
using static AudioLab.Audio.Tests.Support.SynthProject;

namespace AudioLab.Audio.Tests;

public sealed class SynthTests
{
    private const int Rate = 48000;

    // A full-velocity note at gain 1 is 0.6; a mono track at the centre is 3 dB down.
    private const double Level = 0.6 * 0.70710678;

    private static RenderedAudio Single(SynthInstrument synth, int pitch, double duration, int velocity = 127, int rate = Rate) =>
        Render(Project(synth, Clip("c", 0, duration + 0.2, [Note(1, pitch, 0, duration, velocity)])), rate);

    [Fact]
    public void ANoteSoundsAtItsPitch()
    {
        RenderedAudio audio = Single(Plain, 69, 1);

        Assert.Equal(440, Frequency(audio, 0.2, 0.9, Rate), 0);
        RenderedAudio low = Single(Plain, 45, 1);
        Assert.Equal(110, Frequency(low, 0.2, 0.9, Rate), 0);
    }

    [Fact]
    public void TheRenderIsAsLongAsTheClipPlusTheSynthsTail()
    {
        RenderedAudio audio = Single(Plain with { Release = 1 }, 60, 1);

        Assert.True(audio.Frames > Rate * 1.2 + 1);
        Assert.True(audio.Frames < Rate * (1.2 + 4));
    }

    [Theory]
    [InlineData(OscillatorWave.Sine, 0.7071)]
    [InlineData(OscillatorWave.Triangle, 0.5774)]
    [InlineData(OscillatorWave.Square, 1.0)]
    [InlineData(OscillatorWave.Sawtooth, 0.5774)]
    public void EachWaveHasItsLevelAtFullVelocity(OscillatorWave wave, double expected)
    {
        RenderedAudio audio = Single(Plain with { Osc1 = wave }, 57, 1);

        double rms = Rms(audio, 0.2, 0.9, Rate);

        Assert.Equal(expected * Level, rms, expected * Level * 0.06);
    }

    [Fact]
    public void AVelocityOfZeroIsSilentAndOneIsStillHeardAndLevelFollowsVelocity()
    {
        Assert.Equal(0, Peak(Single(Plain, 60, 0.5, velocity: 0), 0, 0.7, Rate));
        Assert.True(Peak(Single(Plain, 60, 0.5, velocity: 1), 0, 0.7, Rate) > 0.01);

        double loud = Rms(Single(Plain, 60, 0.5, velocity: 127), 0.1, 0.4, Rate);
        double softer = Rms(Single(Plain, 60, 0.5, velocity: 64), 0.1, 0.4, Rate);

        Assert.Equal(127 / 64.0, loud / softer, 2);
    }

    [Fact]
    public void GainScalesTheOutputAndTheSecondOscillatorOnlyCountsWhenItsMixIsAboveZero()
    {
        double full = Rms(Single(Plain, 60, 0.5), 0.1, 0.4, Rate);
        double half = Rms(Single(Plain with { Gain = 0.5 }, 60, 0.5), 0.1, 0.4, Rate);
        RenderedAudio withoutSecond = Single(Plain with { Osc2 = OscillatorWave.Square }, 60, 0.5);
        RenderedAudio otherWave = Single(Plain with { Osc2 = OscillatorWave.Sawtooth, Ratio2 = 3 }, 60, 0.5);
        RenderedAudio audible = Single(Plain with { Osc2 = OscillatorWave.Square, Mix2 = 0.5 }, 60, 0.5);

        Assert.Equal(0.5, half / full, 3);
        Assert.Equal(withoutSecond.Left, otherWave.Left);
        Assert.NotEqual(withoutSecond.Left, audible.Left);
    }

    [Fact]
    public void TheCutoffShapesTheToneAndTheResonanceBoostsItsPeak()
    {
        SynthInstrument saw = Plain with { Osc1 = OscillatorWave.Sawtooth };
        double open = Rms(Single(saw, 60, 0.6), 0.2, 0.5, Rate);
        double closed = Rms(Single(saw with { Cutoff = 100 }, 60, 0.6), 0.2, 0.5, Rate);
        float flat = Peak(Single(saw with { Cutoff = 262, Resonance = 0.3 }, 60, 0.6), 0.2, 0.5, Rate);
        float resonant = Peak(Single(saw with { Cutoff = 262, Resonance = 15 }, 60, 0.6), 0.2, 0.5, Rate);

        Assert.True(closed < open * 0.5);
        Assert.True(resonant > flat * 1.5);
    }

    [Fact]
    public void TheCutoffSweepsUpOverTheAttackByTheFilterEnvelopeAndStaysPutWithNone()
    {
        SynthInstrument swept = Plain with { Osc1 = OscillatorWave.Sawtooth, Cutoff = 300, FilterEnv = 3, Attack = 0.4 };

        RenderedAudio sweeping = Single(swept, 36, 1.5);
        RenderedAudio still = Single(swept with { FilterEnv = 0 }, 36, 1.5);

        Assert.True(Brightness(sweeping, 0.5, 0.7) > Brightness(sweeping, 0.0, 0.05) * 1.5);
        Assert.InRange(Brightness(still, 0.5, 0.7) / Brightness(still, 0.0, 0.05), 0.9, 1.1);
    }

    // Audit row 20: a note no longer than the attack was silent, and a note released during its attack lost the attack.
    [Fact]
    public void ANoteShorterThanTheAttackIsHeardAndItsReleaseGoesOnFromWhereTheAttackHadReached()
    {
        SynthInstrument slow = Plain with { Attack = 0.5, Release = 0.2 };
        RenderedAudio audio = Single(slow with { Osc1 = OscillatorWave.Sine }, 45, 0.1);

        float peak = Peak(audio, 0, 1, Rate);

        Assert.True(peak > 0.05, "The note was silent.");
        float biggestStep = 0;
        for (int i = 1; i < audio.Frames; i++)
        {
            biggestStep = Math.Max(biggestStep, Math.Abs(audio.Left[i] - audio.Left[i - 1]));
        }
        Assert.True(biggestStep < peak * 0.1, "The release jumped.");
    }

    [Fact]
    public void ANoteIsLetGoAtItsEndAndDiesAwayWithinTheTail()
    {
        SynthInstrument held = Plain with { Release = 0.4 };
        RenderedAudio audio = Single(held, 60, 0.5);

        Assert.True(Rms(audio, 0.3, 0.5, Rate) > 0.2);
        Assert.True(Rms(audio, 0.5, 0.6, Rate) > Rms(audio, 0.9, 1.0, Rate) * 2);
        Assert.True(Peak(audio, (audio.Frames / (double)Rate) - 0.05, audio.Frames / (double)Rate, Rate) < 1e-3);
    }

    [Fact]
    public void TheSustainLevelHoldsAfterTheDecay()
    {
        SynthInstrument shaped = Plain with { Decay = 0.2, Sustain = 0.4 };
        RenderedAudio audio = Single(shaped, 60, 2);

        double attackPeak = Rms(audio, 0.0, 0.03, Rate);
        double sustained = Rms(audio, 1.5, 1.9, Rate);

        Assert.Equal(0.4, sustained / (Level * 0.70710678), 2);
        Assert.True(attackPeak > sustained * 2);
    }

    // Audit row 31: a note held by the pedal kept sounding through a later clip that covered it.
    [Fact]
    public void ANoteHeldByThePedalIsCutWhereALaterClipStartsOverIt()
    {
        MidiClip first = Clip("a", 0, 4, [Note(1, 60, 0, 0.5)], new MidiControlChange(0.2, 64, 127), new MidiControlChange(3.5, 64, 0));
        MidiClip cover = Clip("b", 1.5, 3, []);

        RenderedAudio covered = Render(Project(Plain, first, cover), tail: false);
        RenderedAudio alone = Render(Project(Plain, first), tail: false);

        Assert.True(Rms(alone, 1.7, 3.0) > 0.2, "The pedal did not hold the note.");
        Assert.True(Rms(covered, 1.0, 1.4) > 0.2, "The note stopped before the later clip.");
        Assert.True(Rms(covered, 1.7, 3.0) < 1e-3, "The note kept sounding through the later clip.");
    }

    [Fact]
    public void ThePedalHoldsANoteUntilItIsUpAndNeverPastTheClip()
    {
        MidiClip held = Clip("a", 0, 2, [Note(1, 60, 0, 0.3)], new MidiControlChange(0.1, 64, 127));
        MidiClip released = Clip("b", 0, 2, [Note(1, 60, 0, 0.3)], new MidiControlChange(0.1, 64, 127), new MidiControlChange(1, 64, 0));

        RenderedAudio untilEnd = Render(Project(Plain, held), tail: false);
        RenderedAudio untilUp = Render(Project(Plain, released), tail: false);

        Assert.True(Rms(untilEnd, 1.5, 1.9) > 0.2);
        Assert.True(Rms(untilUp, 0.6, 0.9) > 0.2);
        Assert.True(Rms(untilUp, 1.2, 1.9) < 1e-3);
    }

    [Fact]
    public void ANoteThatStartsUnderALaterClipDoesNotSoundAndAMutedClipCoversNothing()
    {
        MidiClip under = Clip("a", 0, 2, [Note(1, 60, 0.5, 0.5)]);
        MidiClip cover = Clip("b", 0.4, 0.6, []);

        Assert.Equal(0, Peak(Render(Project(Plain, under, cover), tail: false), 0, 2));
        Assert.True(Peak(Render(Project(Plain, under, cover with { Muted = true }), tail: false), 0, 2) > 0.3);
        Assert.Equal(0, Peak(Render(Project(Plain, under with { Muted = true }), tail: false), 0, 2));
    }

    [Fact]
    public void ANoteIsCutAtItsClipEnd()
    {
        MidiClip clip = Clip("a", 1, 1, [Note(1, 60, 0.5, 5)]);

        RenderedAudio audio = Render(Project(Plain, clip), tail: false);

        Assert.True(Rms(audio, 1.6, 1.9) > 0.2);
        Assert.True(audio.Frames <= (int)Math.Round(2.0 * Rendering.Rate));
    }

    [Fact]
    public void NotesOfOneProjectAreTheSameWhateverTheBlockSize()
    {
        ProjectDocument project = BusyScore();
        RenderedAudio reference = Render(project, Rate, 512);

        foreach (int block in new[] { 1, 7, 64, 480, 4096, 1_000_000 })
        {
            RenderedAudio other = Render(project, Rate, block);
            Assert.Equal(reference.Left, other.Left);
            Assert.Equal(reference.Right, other.Right);
        }
    }

    [Fact]
    public void PullingBlocksOfUnevenLengthGivesTheSameAudioAsOneRender()
    {
        ProjectDocument project = BusyScore();
        RenderedAudio reference = Render(project, Rate, 512, tail: false);
        RenderPlan plan = RenderPlan.Create(
            project,
            new MemoryMedia(),
            new PlanOptions { SampleRate = Rate, Sources = new InstrumentSourceFactory() });
        MixerGraph graph = new(plan, 100);
        float[] left = new float[reference.Frames];
        float[] right = new float[reference.Frames];
        int[] sizes = [1, 99, 333, 4000, 17, 700, 256, 255, 257];

        for (int done = 0, turn = 0; done < left.Length; turn++)
        {
            int count = Math.Min(sizes[turn % sizes.Length], left.Length - done);
            graph.Render(left.AsSpan(done, count), right.AsSpan(done, count));
            done += count;
        }

        Assert.Equal(reference.Left, left);
    }

    [Fact]
    public void TwoRendersAreIdenticalAndASeekBackReplaysTheSameSound()
    {
        ProjectDocument project = BusyScore();
        Assert.Equal(Render(project, Rate).Left, Render(project, Rate).Left);

        RenderPlan plan = RenderPlan.Create(
            project,
            new MemoryMedia(),
            new PlanOptions { SampleRate = Rate, Sources = new InstrumentSourceFactory() });
        MixerGraph graph = new(plan);
        float[] first = new float[Rate];
        float[] again = new float[Rate];
        float[] right = new float[Rate];

        graph.Render(first, right);
        graph.Seek(0);
        graph.Render(again, right);

        Assert.Equal(first, again);
    }

    [Fact]
    public void ASeekIntoANoteContinuesItAtOnceAndLeavesTheOnesThatEndedAlone()
    {
        MidiClip clip = Clip("a", 0, 6, [Note(1, 60, 0, 1), Note(2, 64, 0.5, 4)], new MidiControlChange(0, 1, 0));
        ProjectDocument project = Project(Plain with { Release = 0.01 }, clip);
        RenderPlan plan = RenderPlan.Create(
            project,
            new MemoryMedia(),
            new PlanOptions { SampleRate = Rate, Sources = new InstrumentSourceFactory() });
        MixerGraph graph = new(plan);
        float[] left = new float[Rate / 10];
        float[] right = new float[Rate / 10];

        graph.Seek(Rate * 2);
        graph.Render(left, right);
        double rms = Math.Sqrt(left.Sum(x => x * (double)x) / left.Length);
        graph.Seek(Rate * 5);
        graph.Render(left, right);

        Assert.InRange(rms, 0.25, 0.35);
        Assert.True(left.All(x => Math.Abs(x) < 1e-3), "A note sounded past its end after a seek.");
    }

    [Fact]
    public void AChasedNoteStartsAtTheSeekFrameWithANewAttack()
    {
        MidiClip clip = Clip("a", 0, 4, [Note(1, 60, 0, 3)]);
        ProjectDocument project = Project(Plain with { Attack = 0.1 }, clip);
        RenderPlan plan = RenderPlan.Create(
            project,
            new MemoryMedia(),
            new PlanOptions { SampleRate = Rate, Sources = new InstrumentSourceFactory() });
        MixerGraph graph = new(plan);
        float[] left = new float[Rate / 5];
        float[] right = new float[Rate / 5];

        graph.Seek(Rate);
        graph.Render(left, right);

        float early = left[..(Rate / 100)].Max(Math.Abs);
        float late = left[(Rate / 10)..].Max(Math.Abs);
        Assert.True(early < late * 0.3, "A chased note did not start with its attack.");
    }

    [Fact]
    public void AllocatingNothingWhileNotesStartEndAndTakeOverVoices()
    {
        List<MidiNote> notes = [];
        for (int i = 0; i < 1500; i++)
        {
            notes.Add(Note(i + 1, 36 + (i % 40), i * 0.04, 0.5 + ((i % 7) * 0.4), 40 + (i % 80)));
        }
        ProjectDocument project = Project(
            InstrumentPresets.CreateSynth("Warm Pad") with { Release = 1 },
            Clip("a", 0, 100, [.. notes], new MidiControlChange(1, 64, 127), new MidiControlChange(9, 64, 0)));
        RenderPlan plan = RenderPlan.Create(
            project,
            new MemoryMedia(),
            new PlanOptions { SampleRate = Rendering.Rate, Sources = new InstrumentSourceFactory() });
        MixerGraph graph = new(plan);
        float[] left = new float[480];
        float[] right = new float[480];

        Assert.True(AllocationProbe.SettlesAtZero(() => graph.Render(left, right)));
    }

    [Fact]
    public void EveryPresetPlaysAScaleWithNoSampleThatIsNotANumber()
    {
        foreach (string name in InstrumentPresets.SynthNames)
        {
            MidiNote[] scale = [.. Enumerable.Range(0, 24).Select(i => Note(i + 1, 40 + (i * 3), i * 0.15, 0.4, 20 + (i * 4)))];

            float[] samples = FromSource(InstrumentPresets.CreateSynth(name), Clip("a", 0, 4, scale), Rate, 4.5);

            Assert.True(samples.All(float.IsFinite), name);
            Assert.InRange(samples.Max(Math.Abs), 0.01, 3);
        }
    }

    [Fact]
    public void RandomSettingsAtAnyRateStayFiniteAndBoundedAcrossEveryPitch()
    {
        Random random = new(5);
        OscillatorWave[] waves = [OscillatorWave.Sine, OscillatorWave.Triangle, OscillatorWave.Square, OscillatorWave.Sawtooth];
        int[] rates = [8000, 22050, 44100, 192000];
        for (int round = 0; round < 60; round++)
        {
            SynthInstrument synth = RandomSynth(random, waves);
            MidiNote[] notes = [.. Enumerable.Range(0, 40).Select(i =>
                Note(i + 1, random.Next(0, 128), random.NextDouble(), 0.01 + random.NextDouble(), random.Next(1, 128)))];
            int rate = rates[random.Next(rates.Length)];

            float[] samples = FromSource(synth, Clip("a", 0, 2, notes), rate, 2.5);

            Assert.True(samples.All(float.IsFinite), $"round {round} at {rate}");
            Assert.True(samples.Max(Math.Abs) < 60, $"round {round} at {rate}");
        }
    }

    [Fact]
    public void ANoteThatTakesAVoiceFromTheOldestHeldNoteWhenNoneIsDying()
    {
        (PolyphonicSource source, float[] left, float[] right) = Rig(40, shortestOneAt: -1);

        source.Render(0, left, right);

        Assert.Equal(PolyphonicSource.MaxHeldVoices, source.CountedVoices);
        Assert.True(source.SoundingVoices <= PolyphonicSource.PoolSize);
        Assert.Equal(8 * Rate / 100, source.OldestHeldFrame);
    }

    [Fact]
    public void ANoteTakesTheDyingVoiceBeforeAHeldOne()
    {
        (PolyphonicSource source, float[] left, float[] right) = Rig(40, shortestOneAt: 30);

        source.Render(0, left, right);

        Assert.Equal(PolyphonicSource.MaxHeldVoices, source.CountedVoices);
        Assert.Equal(7 * Rate / 100, source.OldestHeldFrame);
    }

    [Fact]
    public void TheFactoryBuildsASourceForASynthAndNothingForASamplerWithNoSampleOrAnInstrumentTrackWithNoInstrument()
    {
        InstrumentSourceFactory factory = new();
        ProjectDocument project = Project(Plain, Clip("a", 0, 1, []));
        Track sampler = InstrumentTrack("s", InstrumentPresets.CreateSampler());

        Assert.NotNull(factory.Create(project, project.Tracks[0], new AudioFormat(Rate, 2), new NoMedia()));
        Assert.Null(factory.Create(project, sampler, new AudioFormat(Rate, 2), new NoMedia()));
        Assert.Null(factory.Create(project, TestProject.Track("x", TrackKind.Instrument), new AudioFormat(Rate, 2), new NoMedia()));
        Assert.Equal(0, Peak(Render(Project(Plain, Clip("a", 0, 1, [])), tail: false), 0, 1));
    }

    [Fact]
    public void AProjectOfMidiClipsAloneHasLengthAndSoundsTheSameOnBothChannels()
    {
        RenderedAudio audio = Single(Plain, 60, 0.5);
        double left = Rms(audio, 0.1, 0.4, Rate);

        Assert.True(audio.Frames > 0);
        Assert.Equal(audio.Left, audio.Right);
        Assert.True(left > 0.2);
    }

    [Fact]
    public void AClipOf130000NotesAndAHundredThousandPedalEventsIsScheduledInOrder()
    {
        MidiNote[] notes = new MidiNote[130_000];
        for (int i = 0; i < notes.Length; i++)
        {
            notes[i] = Note(i + 1, i % 128, i * 0.01, 0.2, 1 + (i % 127));
        }
        MidiControlChange[] pedal = new MidiControlChange[100_000];
        for (int i = 0; i < pedal.Length; i++)
        {
            pedal[i] = new MidiControlChange(i * 0.0129, 64, i % 2 == 0 ? 127 : 0);
        }
        MidiClip clip = Clip("a", 0, 1400, notes, pedal);

        NoteEvent[] schedule = NoteSchedule.Build([clip], Rate);

        Assert.Equal(130_000, schedule.Length);
        Assert.True(schedule.Zip(schedule.Skip(1)).All(pair => pair.First.OnFrame <= pair.Second.OnFrame));
    }

    private static SynthInstrument RandomSynth(Random random, OscillatorWave[] waves) => Plain with
    {
        Osc1 = waves[random.Next(4)],
        Osc2 = waves[random.Next(4)],
        Ratio2 = random.Next(3) == 0 ? 16 : 0.01 + (random.NextDouble() * 4),
        Mix2 = random.NextDouble(),
        Detune = random.NextDouble() * 50,
        Cutoff = 100 + (random.NextDouble() * 11900),
        Resonance = random.Next(3) == 0 ? 15 : 0.3 + (random.NextDouble() * 14.7),
        FilterEnv = random.NextDouble() * 3,
        Attack = 0.001 + (random.NextDouble() * 0.05),
        Decay = 0.01 + (random.NextDouble() * 0.3),
        Sustain = random.NextDouble(),
        Release = 0.01 + (random.NextDouble() * 0.3),
        Gain = random.NextDouble(),
    };

    // What the source itself makes, before the mixer, which would hide a sample that is not a number by making it silence.
    private static float[] FromSource(SynthInstrument synth, MidiClip clip, int rate, double seconds)
    {
        PolyphonicSource source = Source(synth, rate, clip);
        float[] left = new float[(int)(seconds * rate)];
        float[] right = new float[left.Length];
        for (int done = 0; done < left.Length; done += 997)
        {
            int count = Math.Min(997, left.Length - done);
            source.Render(done, left.AsSpan(done, count), right.AsSpan(done, count));
        }
        return left;
    }

    // How much of a signal is fast change: the root mean square of its steps over its own, which grows with the high frequencies.
    private static double Brightness(RenderedAudio audio, double from, double to)
    {
        int start = (int)Math.Round(from * Rate);
        int end = (int)Math.Round(to * Rate);
        double steps = 0;
        double level = 0;
        for (int i = start + 1; i < end; i++)
        {
            steps += Math.Pow(audio.Left[i] - audio.Left[i - 1], 2);
            level += Math.Pow(audio.Left[i], 2);
        }
        return Math.Sqrt(steps / level);
    }

    private static ProjectDocument BusyScore()
    {
        Random random = new(3);
        MidiNote[] notes = [.. Enumerable.Range(0, 120).Select(i =>
            Note(i + 1, random.Next(30, 100), random.NextDouble() * 4, 0.05 + (random.NextDouble() * 1.5), random.Next(1, 128)))];
        MidiControlChange[] pedal = [new MidiControlChange(0.5, 64, 127), new MidiControlChange(1.7, 64, 0), new MidiControlChange(2.5, 64, 100)];
        MidiClip later = Clip("b", 1.2, 1.5, [Note(1, 72, 0.1, 0.4)]);
        return Project(InstrumentPresets.CreateSynth("Saw Lead"), Clip("a", 0, 4.5, notes, pedal), later);
    }

    // A source that plays the given number of notes, one every 10 ms, each held 5 s; the one at shortestOneAt, if it is 0 or more, lasts 50 ms.
    private static (PolyphonicSource Source, float[] Left, float[] Right) Rig(int count, int shortestOneAt)
    {
        SynthInstrument synth = Plain with { Release = 3, Attack = 0.001 };
        MidiNote[] notes = [.. Enumerable.Range(0, count).Select(i =>
            Note(i + 1, 40 + i, i * 0.01, i == shortestOneAt ? 0.05 : 5))];
        PolyphonicSource source = Source(synth, Rate, Clip("a", 0, 10, notes));
        int frames = (count * Rate / 100) + 200;
        return (source, new float[frames], new float[frames]);
    }

    private sealed class NoMedia : IPlanMedia
    {
        public PcmAudio? Get(MediaId id) => null;
    }

    [Fact]
    public void AHeldPluckThatHasDecayedToNothingFreesItsVoiceAndLeavesExactSilence()
    {
        MidiClip clip = Clip("a", 0, 40, [Note(1, 60, 0, 35)], new MidiControlChange(0.1, 64, 127));
        PolyphonicSource source = Source(InstrumentPresets.CreateSynth("Pluck"), 8000, clip);
        float[] left = new float[8000 * 30];
        float[] right = new float[left.Length];

        for (int done = 0; done < left.Length; done += 800)
        {
            source.Render(done, left.AsSpan(done, 800), right.AsSpan(done, 800));
        }

        Assert.Equal(0, source.SoundingVoices);
        Assert.All(left.AsSpan(8000 * 5).ToArray(), sample => Assert.Equal(0f, sample));
    }

    [Fact]
    public void RepeatedNotesOfOnePitchEachTakeANewVoiceAsTheOldDawDid()
    {
        MidiNote[] notes = [.. Enumerable.Range(0, 5).Select(i => Note(i + 1, 60, i * 0.01, 3))];
        PolyphonicSource source = Source(Plain, Rate, Clip("a", 0, 4, notes));
        float[] left = new float[Rate / 10];
        float[] right = new float[left.Length];

        source.Render(0, left, right);

        Assert.Equal(5, source.CountedVoices);
    }

    [Fact]
    public void TheCutoffSweepGoesOnThroughTheReleaseAsTheOldGraphsDid()
    {
        SynthInstrument slow = Plain with { Osc1 = OscillatorWave.Sawtooth, Cutoff = 300, FilterEnv = 3, Attack = 0.6, Release = 4 };

        RenderedAudio audio = Single(slow, 36, 0.1);

        Assert.True(Brightness(audio, 0.6, 0.7) > Brightness(audio, 0.15, 0.2) * 1.3, "The sweep stopped when the note was let go.");
    }

    [Fact]
    public void AfterASeekTheNotesAreAsOldAsTheyWereSoTheSameVoicesAreTakenAsInContinuousPlay()
    {
        MidiNote[] notes = [.. Enumerable.Range(0, 40).Select(i => Note(i + 1, 40 + i, i * 0.01, 5))];
        MidiClip clip = Clip("a", 0, 10, notes, new MidiControlChange(0, 64, 127));
        PolyphonicSource continuous = Source(Plain with { Release = 3 }, Rate, clip);
        PolyphonicSource seeked = Source(Plain with { Release = 3 }, Rate, clip);
        float[] left = new float[(int)(0.45 * Rate)];
        float[] right = new float[left.Length];
        continuous.Render(0, left, right);

        seeked.Render(left.Length, new float[1], new float[1]);

        Assert.Equal(continuous.OldestHeldFrame, seeked.OldestHeldFrame);
        Assert.Equal(continuous.CountedVoices, seeked.CountedVoices);
    }
}
