using AudioLab.Core.Model;

namespace AudioLab.Core.Migration;

/// <summary>Instruments and effects. A value the extension would have defaulted gets the same default here.</summary>
internal static class V3Sound
{
    public static Instrument? ConvertInstrument(V3Instrument? raw, string path, V3Context context)
    {
        if (raw is null)
        {
            return null;
        }
        return raw.Type switch
        {
            null or "synth" => ConvertSynth(raw, path),
            "sampler" => ConvertSampler(raw, path, context),
            _ => throw V3Context.Fail($"{path}.type", $"'{raw.Type}' is not synth or sampler."),
        };
    }

    public static List<Effect> ConvertEffects(List<V3Fx> raw, string path, V3Context context)
    {
        List<Effect> effects = [];
        for (int i = 0; i < raw.Count; i++)
        {
            string fxPath = $"{path}[{i}]";
            V3Params values = new(raw[i].Params, fxPath);
            EffectId id = context.Ids.Next<EffectId>();
            bool enabled = raw[i].Enabled ?? true;
            Effect effect = raw[i].Type switch
            {
                "eq" => Eq(id, enabled, values),
                "compressor" => Compressor(id, enabled, values),
                "reverb" => Reverb(id, enabled, values),
                "delay" => Delay(id, enabled, values),
                "saturation" => Saturation(id, enabled, values),
                _ => throw V3Context.Fail($"{fxPath}.type", $"'{raw[i].Type}' is not an effect version 4 knows."),
            };
            values.RejectUnknown();
            effects.Add(effect);
        }
        return effects;
    }

    private static SynthInstrument ConvertSynth(V3Instrument raw, string path)
    {
        if (raw.Name is not null || raw.SampleKey is not null || raw.SampleName is not null || raw.RootNote is not null || raw.OneShot is not null)
        {
            throw V3Context.Fail(path, "a synth has sampler fields, so it is not clear which it is.");
        }
        return new SynthInstrument
        {
            Preset = string.IsNullOrEmpty(raw.Preset) ? "Custom" : raw.Preset,
            Osc1 = Wave(raw.Osc1, $"{path}.osc1"),
            Osc2 = Wave(raw.Osc2, $"{path}.osc2"),
            Ratio2 = OrDefault(raw.Ratio2, 1),
            Mix2 = raw.Mix2 ?? 0.5,
            Detune = OrDefault(raw.Detune, 0),
            Cutoff = OrDefault(raw.Cutoff, 3000),
            Resonance = raw.Resonance ?? 1,
            FilterEnv = OrDefault(raw.FilterEnv, 0),
            Attack = OrDefault(raw.Attack, 0.005),
            Decay = OrDefault(raw.Decay, 0.3),
            Sustain = raw.Sustain ?? 0.5,
            Release = OrDefault(raw.Release, 0.2),
            Gain = raw.Gain ?? 0.5,
        };
    }

    private static SamplerInstrument ConvertSampler(V3Instrument raw, string path, V3Context context)
    {
        bool hasSynthField = raw.Preset is not null || raw.Osc1 is not null || raw.Osc2 is not null || raw.Ratio2 is not null
            || raw.Mix2 is not null || raw.Detune is not null || raw.Cutoff is not null || raw.Resonance is not null
            || raw.FilterEnv is not null || raw.Decay is not null || raw.Sustain is not null;
        if (hasSynthField)
        {
            throw V3Context.Fail(path, "a sampler has synth fields, so it is not clear which it is.");
        }
        return new SamplerInstrument
        {
            Name = string.IsNullOrEmpty(raw.Name) ? "Sampler" : raw.Name,
            Sample = string.IsNullOrEmpty(raw.SampleKey) ? null : context.MediaFor(raw.SampleKey),
            SampleName = raw.SampleName ?? string.Empty,
            RootNote = OrDefault(raw.RootNote, 60),
            Attack = OrDefault(raw.Attack, 0.002),
            Release = OrDefault(raw.Release, 0.2),
            Gain = raw.Gain ?? 0.8,
            OneShot = raw.OneShot == true,
        };
    }

    private static OscillatorWave Wave(string? name, string path)
    {
        return name switch
        {
            null or "sawtooth" => OscillatorWave.Sawtooth,
            "sine" => OscillatorWave.Sine,
            "triangle" => OscillatorWave.Triangle,
            "square" => OscillatorWave.Square,
            _ => throw V3Context.Fail(path, $"'{name}' is not sine, triangle, square or sawtooth."),
        };
    }

    // The extension reads some synth fields with ||, so a stored 0 means "use the default" there.
    private static double OrDefault(double? value, double fallback) => value is { } number && number != 0 ? number : fallback;

    private static int OrDefault(int? value, int fallback) => value is { } number && number != 0 ? number : fallback;

    private static EqEffect Eq(EffectId id, bool enabled, V3Params p)
    {
        return new EqEffect
        {
            Id = id,
            Enabled = enabled,
            HpFreq = p.Number("hpFreq", 20),
            LowGain = p.Number("lowGain", 0),
            LowFreq = p.Number("lowFreq", 200),
            MidGain = p.Number("midGain", 0),
            MidFreq = p.Number("midFreq", 1000),
            MidQ = p.Number("midQ", 1),
            HighGain = p.Number("highGain", 0),
            HighFreq = p.Number("highFreq", 4000),
            LpFreq = p.Number("lpFreq", 20000),
        };
    }

    private static CompressorEffect Compressor(EffectId id, bool enabled, V3Params p)
    {
        return new CompressorEffect
        {
            Id = id,
            Enabled = enabled,
            Threshold = p.Number("threshold", -24),
            Knee = p.Number("knee", 30),
            Ratio = p.Number("ratio", 4),
            Attack = p.Number("attack", 0.003),
            Release = p.Number("release", 0.25),
            Makeup = p.Number("makeup", 0),
        };
    }

    private static ReverbEffect Reverb(EffectId id, bool enabled, V3Params p)
    {
        return new ReverbEffect
        {
            Id = id,
            Enabled = enabled,
            Size = p.Number("size", 2),
            Decay = p.Number("decay", 3),
            Predelay = p.Number("predelay", 0.02),
            Mix = p.Number("mix", 0.3),
        };
    }

    private static DelayEffect Delay(EffectId id, bool enabled, V3Params p)
    {
        return new DelayEffect
        {
            Id = id,
            Enabled = enabled,
            Time = p.Number("time", 0.35),
            Sync = p.Flag("sync", false),
            Division = p.Text("division", "1/4"),
            Feedback = p.Number("feedback", 0.35),
            Tone = p.Number("tone", 8000),
            Mix = p.Number("mix", 0.25),
        };
    }

    private static SaturationEffect Saturation(EffectId id, bool enabled, V3Params p)
    {
        return new SaturationEffect
        {
            Id = id,
            Enabled = enabled,
            Drive = p.Number("drive", 0.3),
            Trim = p.Number("trim", 0),
            Mix = p.Number("mix", 1),
        };
    }
}
