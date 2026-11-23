using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Audio.Instruments;

/// <summary>A sampler's settings and its sample, turned once into what a voice uses: the pitch ratio, frames and levels.</summary>
/// <remarks>
/// Each numeric setting is held to the range of <see cref="InstrumentRules"/>, as for the synth. The sample is at the plan's rate and
/// has one or two channels, which the plan's media gives. The envelope is the old DAW's: linear over the attack to the level, which is
/// the gain times the velocity, and a release with a time constant of a quarter of the release setting from the level reached.
/// </remarks>
internal sealed class SamplerSettings
{
    /// <summary>The longest a one-shot sample sounds after its note is let go, in seconds; it fades out over its last 50 ms.</summary>
    public const double OneShotLimit = 30;

    /// <summary>How long the last stretch of a one-shot that reaches its limit takes to fade out, in seconds.</summary>
    public const double LimitFade = 0.05;

    /// <summary>Settles the settings of <paramref name="sampler"/> and its <paramref name="sample"/>.</summary>
    public SamplerSettings(SamplerInstrument sampler, PcmAudio sample)
    {
        ArgumentNullException.ThrowIfNull(sampler);
        ArgumentNullException.ThrowIfNull(sample);
        Sample = sample;
        int rate = sample.SampleRate;
        RootNote = Math.Clamp(sampler.RootNote, InstrumentRules.LowestNote, InstrumentRules.HighestNote);
        OneShot = sampler.OneShot;
        Gain = Value(sampler, "gain");
        AttackFrames = Math.Max(1, (long)Math.Round(Value(sampler, "attack") * rate));
        double release = Value(sampler, "release");
        ReleaseCoefficient = Math.Exp(-1 / (release / 4 * rate));
        ReleaseTail = Gain > SynthSettings.SilentLevel ? release / 4 * Math.Log(Gain / SynthSettings.SilentLevel) : 0;
        FadeFrames = Math.Max(1, (int)Math.Round(SynthSettings.StealFade * rate));
        LimitFrames = (long)Math.Round(OneShotLimit * rate);
        LimitFadeFrames = Math.Max(1, (long)Math.Round(LimitFade * rate));
    }

    /// <summary>The sample that is played.</summary>
    public PcmAudio Sample { get; }

    /// <summary>The MIDI note at which the sample plays at its own pitch.</summary>
    public int RootNote { get; }

    /// <summary>Whether a note plays the whole sample however long it is held.</summary>
    public bool OneShot { get; }

    /// <summary>The level of a voice at full velocity.</summary>
    public double Gain { get; }

    /// <summary>How many frames the attack lasts, at least one.</summary>
    public long AttackFrames { get; }

    /// <summary>The factor the level shrinks by each frame of the release.</summary>
    public double ReleaseCoefficient { get; }

    /// <summary>How long a note at full velocity takes to fall below <see cref="SynthSettings.SilentLevel"/> once it is let go, in seconds.</summary>
    public double ReleaseTail { get; }

    /// <summary>How many frames a voice that is taken over takes to fade out.</summary>
    public int FadeFrames { get; }

    /// <summary>How many frames after its note a one-shot may play at most.</summary>
    public long LimitFrames { get; }

    /// <summary>How many frames the end of a one-shot that reaches that limit takes to fade out.</summary>
    public long LimitFadeFrames { get; }

    /// <summary>How many times faster than its own pitch the sample plays for a note.</summary>
    public double Ratio(int pitch) => Math.Pow(2, (pitch - RootNote) / 12.0);

    /// <summary>The frame a note stops sounding: its release or where its sample ends, or for a one-shot its sample's end or its limit.</summary>
    public long SoundingEnd(NoteEvent note)
    {
        long sampleEnd = note.OnFrame + (long)Math.Ceiling(Sample.Frames / Ratio(note.Pitch));
        return OneShot ? Math.Min(sampleEnd, Math.Max(note.OffFrame, note.OnFrame) + LimitFrames) : Math.Min(sampleEnd, note.OffFrame);
    }

    /// <summary>How long the source sounds past <paramref name="contentEnd"/>, where the project's audible content ends, in seconds.</summary>
    /// <remarks>It is the latest end of any note: where its sample ends, or its release has died away, or for a one-shot the limit.</remarks>
    public double TailSeconds(NoteEvent[] notes, double contentEnd)
    {
        int rate = Sample.SampleRate;
        double last = 0;
        foreach (NoteEvent note in notes)
        {
            double on = note.OnFrame / (double)rate;
            double off = note.OffFrame / (double)rate;
            double sampleEnd = on + (Sample.Frames / (double)rate / Ratio(note.Pitch));
            double end = Math.Min(sampleEnd, OneShot ? off + OneShotLimit : off + ReleaseTail);
            last = Math.Max(last, end);
        }
        return Math.Max(0, last - contentEnd);
    }

    private static double Value(SamplerInstrument sampler, string name)
    {
        InstrumentParameter parameter = InstrumentRules.Find(sampler, name)
            ?? throw new InvalidOperationException($"The sampler has no setting called {name}.");
        double value = parameter.Get(sampler);
        return double.IsFinite(value) ? Math.Clamp(value, parameter.Min, parameter.Max) : parameter.Min;
    }
}
