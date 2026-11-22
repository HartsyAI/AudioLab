namespace AudioLab.Core.Model;

/// <summary>A polyphonic synth: two oscillators, a low-pass filter with an envelope, an amplitude envelope.</summary>
public sealed record SynthInstrument : Instrument
{
    /// <summary>The preset the sound started from, such as Soft Keys.</summary>
    public required string Preset { get; init; }

    /// <summary>The first oscillator's wave.</summary>
    public required OscillatorWave Osc1 { get; init; }

    /// <summary>The second oscillator's wave.</summary>
    public required OscillatorWave Osc2 { get; init; }

    /// <summary>The second oscillator's pitch as a multiple of the first.</summary>
    public required double Ratio2 { get; init; }

    /// <summary>How much of the second oscillator is mixed in, 0 to 1.</summary>
    public required double Mix2 { get; init; }

    /// <summary>The detune between the oscillators, in cents.</summary>
    public required double Detune { get; init; }

    /// <summary>The filter cutoff, in hertz.</summary>
    public required double Cutoff { get; init; }

    /// <summary>The filter resonance.</summary>
    public required double Resonance { get; init; }

    /// <summary>How far the filter envelope sweeps the cutoff.</summary>
    public required double FilterEnv { get; init; }

    /// <summary>The attack time, in seconds.</summary>
    public required double Attack { get; init; }

    /// <summary>The decay time, in seconds.</summary>
    public required double Decay { get; init; }

    /// <summary>The sustain level, 0 to 1.</summary>
    public required double Sustain { get; init; }

    /// <summary>The release time, in seconds.</summary>
    public required double Release { get; init; }

    /// <summary>The output level, 0 to 1.</summary>
    public required double Gain { get; init; }
}
