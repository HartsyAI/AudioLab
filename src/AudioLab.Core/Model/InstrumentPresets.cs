namespace AudioLab.Core.Model;

/// <summary>The starting sound sources of an instrument track: seven synth presets and an empty sampler.</summary>
public static class InstrumentPresets
{
    /// <summary>The name of the sampler an instrument track can start with.</summary>
    public const string Sampler = "Sampler";

    /// <summary>The preset an instrument track gets when none is asked for.</summary>
    public const string DefaultSynth = "Soft Keys";

    private static readonly Dictionary<string, SynthInstrument> _synths = new(StringComparer.Ordinal)
    {
        ["Soft Keys"] = Synth("Soft Keys", OscillatorWave.Triangle, OscillatorWave.Sine, 2, 0.25, 0, 3200, 1, 0.6, 0.004, 0.7, 0.25, 0.45, 0.55),
        ["Warm Pad"] = Synth("Warm Pad", OscillatorWave.Sawtooth, OscillatorWave.Sawtooth, 1, 0.8, 14, 1100, 2, 0.3, 0.5, 1.2, 0.8, 1.4, 0.35),
        ["Sub Bass"] = Synth("Sub Bass", OscillatorWave.Sine, OscillatorWave.Square, 0.5, 0.18, 0, 700, 1, 0.2, 0.004, 0.25, 0.9, 0.12, 0.7),
        ["Saw Lead"] = Synth("Saw Lead", OscillatorWave.Sawtooth, OscillatorWave.Sawtooth, 1, 0.6, 9, 3800, 6, 0.5, 0.01, 0.3, 0.7, 0.2, 0.35),
        ["Pluck"] = Synth("Pluck", OscillatorWave.Square, OscillatorWave.Sawtooth, 1, 0.5, 4, 1600, 3, 1.6, 0.002, 0.28, 0, 0.18, 0.45),
        ["Organ"] = Synth("Organ", OscillatorWave.Sine, OscillatorWave.Sine, 2, 0.55, 0, 6000, 0.5, 0, 0.01, 0.1, 1, 0.08, 0.45),
        ["Bell"] = Synth("Bell", OscillatorWave.Sine, OscillatorWave.Sine, 2.76, 0.6, 0, 8000, 0.5, 0, 0.002, 1.6, 0, 1.2, 0.45),
    };

    /// <summary>The synth preset names, in the order a menu lists them.</summary>
    public static IReadOnlyList<string> SynthNames { get; } = [.. _synths.Keys];

    /// <summary>Every name an instrument track can start with: the synth presets, then the sampler.</summary>
    public static IReadOnlyList<string> Names { get; } = [.. _synths.Keys, Sampler];

    /// <summary>A synth with the named preset's sound; an unknown name gets <see cref="DefaultSynth"/>.</summary>
    public static SynthInstrument CreateSynth(string preset)
    {
        ArgumentNullException.ThrowIfNull(preset);
        return _synths.TryGetValue(preset, out SynthInstrument? synth) ? synth : _synths[DefaultSynth];
    }

    /// <summary>An empty sampler: no sample yet, rooted at middle C.</summary>
    public static SamplerInstrument CreateSampler()
    {
        return new SamplerInstrument
        {
            Name = Sampler,
            SampleName = string.Empty,
            RootNote = 60,
            Attack = 0.002,
            Release = 0.2,
            Gain = 0.8,
            OneShot = false,
        };
    }

    private static SynthInstrument Synth(
        string preset,
        OscillatorWave osc1,
        OscillatorWave osc2,
        double ratio2,
        double mix2,
        double detune,
        double cutoff,
        double resonance,
        double filterEnv,
        double attack,
        double decay,
        double sustain,
        double release,
        double gain)
    {
        return new SynthInstrument
        {
            Preset = preset,
            Osc1 = osc1,
            Osc2 = osc2,
            Ratio2 = ratio2,
            Mix2 = mix2,
            Detune = detune,
            Cutoff = cutoff,
            Resonance = resonance,
            FilterEnv = filterEnv,
            Attack = attack,
            Decay = decay,
            Sustain = sustain,
            Release = release,
            Gain = gain,
        };
    }
}
