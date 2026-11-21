using AudioLab.Core.Model;

namespace AudioLab.Core.Migration;

/// <summary>Instruments and effects. A value the extension would have defaulted gets the same default here.</summary>
internal static class V3Sound
{
    /// <summary>An instrument with the extension's defaults for what was left out; null for a track with none.</summary>
    public static Instrument? ConvertInstrument(V3Instrument? raw, string path, V3Context context)
    {
        if (raw is null)
        {
            return null;
        }
        return raw.Type switch
        {
            null or "synth" => ConvertSynth(raw, path, context),
            "sampler" => ConvertSampler(raw, path, context),
            _ => throw V3Context.Fail($"{path}.type", $"'{raw.Type}' is not synth or sampler."),
        };
    }

    /// <summary>An insert chain; an unknown effect or parameter is refused, a missing one takes its default.</summary>
    public static List<Effect> ConvertEffects(List<V3Fx> raw, string path, V3Context context)
    {
        List<Effect> effects = [];
        for (int i = 0; i < raw.Count; i++)
        {
            string fxPath = $"{path}[{i}]";
            V3Params values = new(raw[i].Params, fxPath);
            EffectId id = context.Ids.Next<EffectId>();
            context.Origin(id.Value, V3Context.Num(i));
            // restoreProject copies the flag as it is, so a missing one reads as off
            bool enabled = raw[i].Enabled == true;
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

    private static SynthInstrument ConvertSynth(V3Instrument raw, string path, V3Context context)
    {
        WarnIgnored(
            path,
            "synth",
            "sampler",
            context,
            ("name", raw.Name),
            ("sampleKey", raw.SampleKey),
            ("sampleName", raw.SampleName),
            ("rootNote", raw.RootNote),
            ("oneShot", raw.OneShot));
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
        WarnIgnored(
            path,
            "sampler",
            "synth",
            context,
            ("preset", raw.Preset),
            ("osc1", raw.Osc1),
            ("osc2", raw.Osc2),
            ("ratio2", raw.Ratio2),
            ("mix2", raw.Mix2),
            ("detune", raw.Detune),
            ("cutoff", raw.Cutoff),
            ("resonance", raw.Resonance),
            ("filterEnv", raw.FilterEnv),
            ("decay", raw.Decay),
            ("sustain", raw.Sustain));
        return new SamplerInstrument
        {
            Name = context.Name(raw.Name, "Sampler", $"{path}.name"),
            Sample = SampleMedia(raw.SampleKey, path, context),
            SampleName = context.Name(raw.SampleName, string.Empty, $"{path}.sampleName"),
            RootNote = OrDefault(raw.RootNote, 60),
            Attack = OrDefault(raw.Attack, 0.002),
            Release = OrDefault(raw.Release, 0.2),
            Gain = raw.Gain ?? 0.8,
            OneShot = raw.OneShot == true,
        };
    }

    // Changing a track's instrument replaces the object in the extension, so a leftover from the other kind is noise.
    private static void WarnIgnored(
        string path,
        string kind,
        string other,
        V3Context context,
        params (string Name, object? Value)[] fields)
    {
        string[] present = [.. fields.Where(field => field.Value is not null).Select(field => field.Name)];
        if (present.Length > 0)
        {
            string names = string.Join(", ", present);
            context.Warn($"{path} is a {kind} but had {other} fields ({names}); they were ignored.");
        }
    }

    private static MediaId? SampleMedia(string? sampleKey, string path, V3Context context)
    {
        if (string.IsNullOrEmpty(sampleKey))
        {
            return null;
        }
        MediaId? media = context.MediaFor(sampleKey, $"{path}.sampleKey");
        if (media is null)
        {
            context.Warn($"{path}: its sample '{sampleKey}' is not among the audio files; the sampler has no sample.");
        }
        return media;
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
    private static double OrDefault(double? value, double fallback)
    {
        return value is { } number && number != 0 ? number : fallback;
    }

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
