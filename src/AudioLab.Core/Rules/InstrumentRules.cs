using AudioLab.Core.Model;

namespace AudioLab.Core.Rules;

/// <summary>The instrument rules: each kind's numeric settings with their ranges, and the notes a sampler may be rooted at.</summary>
public static class InstrumentRules
{
    /// <summary>The lowest MIDI note a sampler may be rooted at, and the lowest pitch there is.</summary>
    public const int LowestNote = 0;

    /// <summary>The highest MIDI note a sampler may be rooted at, and the highest pitch there is.</summary>
    public const int HighestNote = 127;

    private static readonly IReadOnlyList<InstrumentParameter> _synth =
    [
        Synth("mix2", 0, 1, s => s.Mix2, (s, v) => s with { Mix2 = v }),
        Synth("detune", 0, 50, s => s.Detune, (s, v) => s with { Detune = v }),
        Synth("cutoff", 100, 12000, s => s.Cutoff, (s, v) => s with { Cutoff = v }),
        Synth("resonance", 0.3, 15, s => s.Resonance, (s, v) => s with { Resonance = v }),
        Synth("filterEnv", 0, 3, s => s.FilterEnv, (s, v) => s with { FilterEnv = v }),
        Synth("attack", 0.001, 2, s => s.Attack, (s, v) => s with { Attack = v }),
        Synth("decay", 0.01, 3, s => s.Decay, (s, v) => s with { Decay = v }),
        Synth("sustain", 0, 1, s => s.Sustain, (s, v) => s with { Sustain = v }),
        Synth("release", 0.01, 4, s => s.Release, (s, v) => s with { Release = v }),
        Synth("gain", 0, 1, s => s.Gain, (s, v) => s with { Gain = v }),
    ];

    private static readonly IReadOnlyList<InstrumentParameter> _sampler =
    [
        Sampler("attack", 0.001, 2, s => s.Attack, (s, v) => s with { Attack = v }),
        Sampler("release", 0.01, 4, s => s.Release, (s, v) => s with { Release = v }),
        Sampler("gain", 0, 1, s => s.Gain, (s, v) => s with { Gain = v }),
    ];

    /// <summary>The name of every numeric setting of either kind, once each.</summary>
    public static IReadOnlyList<string> AllParameterNames { get; } = [.. _synth.Concat(_sampler).Select(setting => setting.Name).Distinct()];

    /// <summary>The numeric settings of an instrument's kind, with their ranges.</summary>
    public static IReadOnlyList<InstrumentParameter> Parameters(Instrument instrument)
    {
        ArgumentNullException.ThrowIfNull(instrument);
        return instrument is SynthInstrument ? _synth : _sampler;
    }

    /// <summary>The setting of that name for the instrument's kind, or null.</summary>
    public static InstrumentParameter? Find(Instrument instrument, string name)
    {
        return Parameters(instrument).FirstOrDefault(setting => setting.Name == name);
    }

    private static InstrumentParameter Synth(
        string name,
        double min,
        double max,
        Func<SynthInstrument, double> get,
        Func<SynthInstrument, double, SynthInstrument> set)
    {
        return new InstrumentParameter(
            name,
            min,
            max,
            instrument => get((SynthInstrument)instrument),
            (instrument, value) => set((SynthInstrument)instrument, value));
    }

    private static InstrumentParameter Sampler(
        string name,
        double min,
        double max,
        Func<SamplerInstrument, double> get,
        Func<SamplerInstrument, double, SamplerInstrument> set)
    {
        return new InstrumentParameter(
            name,
            min,
            max,
            instrument => get((SamplerInstrument)instrument),
            (instrument, value) => set((SamplerInstrument)instrument, value));
    }
}
