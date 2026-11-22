using AudioLab.Core.Model;

namespace AudioLab.Core.Rules;

/// <summary>The starter presets of each effect kind, as data: a name and the values it lays over the kind's defaults.</summary>
public static class EffectPresets
{
    private static readonly Dictionary<EffectKind, IReadOnlyList<EffectPreset>> _byKind = new()
    {
        [EffectKind.Eq] =
        [
            Preset("Vocal clarity", ("hpFreq", 90), ("lowGain", -2), ("midGain", 2.5), ("midFreq", 3000), ("midQ", 1.2), ("highGain", 2), ("highFreq", 8000)),
            Preset("Drum punch", ("hpFreq", 40), ("lowGain", 3), ("lowFreq", 80), ("midGain", -2), ("midFreq", 400), ("highGain", 2.5), ("highFreq", 6000)),
            Preset("Bass focus", ("hpFreq", 30), ("lowGain", 4), ("lowFreq", 70), ("midGain", -1.5), ("midFreq", 800), ("lpFreq", 8000)),
            Preset("Telephone", ("hpFreq", 600), ("lpFreq", 3000), ("midGain", 4), ("midFreq", 1500)),
        ],
        [EffectKind.Compressor] =
        [
            Preset("Vocal level", ("threshold", -20), ("ratio", 3), ("attack", 0.01), ("release", 0.2), ("makeup", 4)),
            Preset("Drum smash", ("threshold", -30), ("ratio", 8), ("attack", 0.002), ("release", 0.1), ("makeup", 6)),
            Preset("Glue (gentle)", ("threshold", -16), ("ratio", 2), ("attack", 0.03), ("release", 0.3), ("makeup", 2)),
        ],
        [EffectKind.Reverb] =
        [
            Preset("Small room", ("size", 0.6), ("decay", 2.5), ("predelay", 0.005), ("mix", 0.18)),
            Preset("Hall", ("size", 3.5), ("decay", 3), ("predelay", 0.03), ("mix", 0.3)),
            Preset("Cathedral", ("size", 7), ("decay", 2), ("predelay", 0.05), ("mix", 0.4)),
        ],
        [EffectKind.Delay] =
        [
            Preset("Slap", false, null, ("time", 0.09), ("feedback", 0.12), ("mix", 0.22)),
            Preset("Quarter echo", true, "1/4", ("feedback", 0.4), ("mix", 0.25)),
            Preset("Dotted 8th", true, "1/8.", ("feedback", 0.45), ("mix", 0.28)),
        ],
        [EffectKind.Saturation] =
        [
            Preset("Warm", ("drive", 0.25), ("trim", -1), ("mix", 0.8)),
            Preset("Crunch", ("drive", 0.6), ("trim", -3), ("mix", 1)),
            Preset("Destroy", ("drive", 1), ("trim", -6), ("mix", 1)),
        ],
    };

    /// <summary>The presets of a kind, in the order a menu lists them.</summary>
    public static IReadOnlyList<EffectPreset> For(EffectKind kind) => _byKind[kind];

    /// <summary>The names of every preset of every kind, once each.</summary>
    public static IReadOnlyList<string> AllNames { get; } = [.. _byKind.Values.SelectMany(presets => presets).Select(preset => preset.Name).Distinct()];

    /// <summary>The preset of a kind with this name, or null.</summary>
    public static EffectPreset? Find(EffectKind kind, string name) => _byKind[kind].FirstOrDefault(preset => preset.Name == name);

    /// <summary>The effect with the preset laid over the kind's defaults; the id and the bypass are kept.</summary>
    public static Effect Apply(Effect effect, EffectPreset preset)
    {
        ArgumentNullException.ThrowIfNull(effect);
        ArgumentNullException.ThrowIfNull(preset);
        Effect result = EffectRules.Create(EffectRules.KindOf(effect), effect.Id) with { Enabled = effect.Enabled };
        foreach (KeyValuePair<string, double> value in preset.Values)
        {
            result = EffectRules.Find(result, value.Key)!.Set(result, value.Value);
        }
        if (result is DelayEffect delay)
        {
            result = delay with { Sync = preset.Sync ?? delay.Sync, Division = preset.Division ?? delay.Division };
        }
        return result;
    }

    private static EffectPreset Preset(string name, params (string Name, double Value)[] values)
    {
        return new EffectPreset(name, values.ToDictionary(value => value.Name, value => value.Value));
    }

    private static EffectPreset Preset(string name, bool sync, string? division, params (string Name, double Value)[] values)
    {
        return new EffectPreset(name, values.ToDictionary(value => value.Name, value => value.Value), sync, division);
    }
}
