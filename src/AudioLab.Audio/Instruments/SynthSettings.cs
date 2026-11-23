using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Audio.Instruments;

/// <summary>A synth's settings turned, once, into what a voice uses at a sample rate: frames, coefficients and levels.</summary>
/// <remarks>
/// Each numeric setting is held to the range of <see cref="InstrumentRules"/> and a value that is not a number becomes the lowest, so
/// a document that skipped the validator cannot make a sample that is not a number. The shapes follow the old DAW's graph: the amplitude
/// rises linearly over the attack, then settles to the sustain with a time constant of a third of the decay, and is let go with a
/// time constant of a quarter of the release; the cutoff rises from its base to the base times one plus three times the filter
/// envelope over the attack and settles with the same rule, where the sustain is the share of the sweep that stays.
/// </remarks>
internal sealed class SynthSettings
{
    /// <summary>The level at which a released voice is taken to have stopped: -80 dB of full scale.</summary>
    public const double SilentLevel = 1e-4;

    /// <summary>The output level of a voice at full velocity before the gain setting, as the old DAW had it.</summary>
    public const double VoiceLevel = 0.6;

    /// <summary>How long a voice that is taken over fades out, in seconds.</summary>
    public const double StealFade = 0.002;

    /// <summary>The frames between two recalculations of the filter, a power of two; each voice counts them from its own start.</summary>
    public const int ControlInterval = 32;

    /// <summary>Settles the settings of <paramref name="synth"/> at <paramref name="sampleRate"/>.</summary>
    public SynthSettings(SynthInstrument synth, int sampleRate)
    {
        ArgumentNullException.ThrowIfNull(synth);
        SampleRate = sampleRate;
        Osc1 = synth.Osc1;
        Osc2 = synth.Osc2;
        double detune = Value(synth, "detune");
        double mix2 = Value(synth, "mix2");
        Ratio2 = double.IsFinite(synth.Ratio2) ? Math.Clamp(synth.Ratio2, 0.001, 16) : 1;
        Level1 = 1 - (mix2 * 0.5);
        Level2 = mix2 > 0.001 ? mix2 : 0;
        Detune1 = Math.Pow(2, -detune / 2 / 1200);
        Detune2 = Math.Pow(2, detune / 2 / 1200);
        double nyquistLimit = sampleRate / 2.2;
        FilterBase = Math.Min(nyquistLimit, Value(synth, "cutoff"));
        FilterPeak = Math.Min(nyquistLimit, FilterBase * (1 + (Value(synth, "filterEnv") * 3)));
        ResonanceDb = Value(synth, "resonance");
        Sustain = Value(synth, "sustain");
        AttackFrames = Math.Max(1, (long)Math.Round(Value(synth, "attack") * sampleRate));
        DecayTauFrames = Value(synth, "decay") / 3 * sampleRate;
        DecayCoefficient = Math.Exp(-1 / DecayTauFrames);
        double release = Value(synth, "release");
        ReleaseCoefficient = Math.Exp(-1 / (release / 4 * sampleRate));
        Gain = Value(synth, "gain") * VoiceLevel;
        FadeFrames = Math.Max(1, (int)Math.Round(StealFade * sampleRate));
        TailSeconds = Gain > SilentLevel ? release / 4 * Math.Log(Gain / SilentLevel) : 0;
    }

    /// <summary>The rate the voices run at.</summary>
    public int SampleRate { get; }

    /// <summary>The first oscillator's wave.</summary>
    public OscillatorWave Osc1 { get; }

    /// <summary>The second oscillator's wave.</summary>
    public OscillatorWave Osc2 { get; }

    /// <summary>The second oscillator's pitch as a multiple of the first.</summary>
    public double Ratio2 { get; }

    /// <summary>How much of the first oscillator is heard.</summary>
    public double Level1 { get; }

    /// <summary>How much of the second oscillator is heard; 0 turns it off.</summary>
    public double Level2 { get; }

    /// <summary>The pitch factor of the first oscillator, half the detune below.</summary>
    public double Detune1 { get; }

    /// <summary>The pitch factor of the second oscillator, half the detune above.</summary>
    public double Detune2 { get; }

    /// <summary>The cutoff at rest, in hertz.</summary>
    public double FilterBase { get; }

    /// <summary>The cutoff at the top of the sweep, in hertz.</summary>
    public double FilterPeak { get; }

    /// <summary>The filter's resonance, in decibels.</summary>
    public double ResonanceDb { get; }

    /// <summary>The level the amplitude settles to, as a share of its peak, and the share of the sweep the cutoff keeps.</summary>
    public double Sustain { get; }

    /// <summary>How many frames the attack lasts, at least one.</summary>
    public long AttackFrames { get; }

    /// <summary>The time constant of the decay, in frames.</summary>
    public double DecayTauFrames { get; }

    /// <summary>The factor the distance to the sustain shrinks by each frame of the decay.</summary>
    public double DecayCoefficient { get; }

    /// <summary>The factor the level shrinks by each frame of the release.</summary>
    public double ReleaseCoefficient { get; }

    /// <summary>The level of a voice at full velocity: the gain times <see cref="VoiceLevel"/>.</summary>
    public double Gain { get; }

    /// <summary>How many frames a voice that is taken over takes to fade out.</summary>
    public int FadeFrames { get; }

    /// <summary>How long a note at full velocity takes to fall below <see cref="SilentLevel"/> once it is let go, in seconds.</summary>
    public double TailSeconds { get; }

    /// <summary>The frequency of a MIDI note, in hertz, with A4 (69) at 440.</summary>
    public static double Frequency(int pitch) => 440 * Math.Pow(2, (pitch - 69) / 12.0);

    private static double Value(SynthInstrument synth, string name)
    {
        InstrumentParameter parameter = InstrumentRules.Find(synth, name)
            ?? throw new InvalidOperationException($"The synth has no setting called {name}.");
        double value = parameter.Get(synth);
        return double.IsFinite(value) ? Math.Clamp(value, parameter.Min, parameter.Max) : parameter.Min;
    }
}
