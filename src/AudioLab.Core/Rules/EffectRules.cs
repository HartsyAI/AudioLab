using AudioLab.Core.Model;

namespace AudioLab.Core.Rules;

/// <summary>The effect rules: each kind's starting settings, its settings and their ranges, and the delay divisions.</summary>
public static class EffectRules
{
    private static readonly Dictionary<string, double> _divisions = new(StringComparer.Ordinal)
    {
        ["1/2"] = 2,
        ["1/4"] = 1,
        ["1/4."] = 1.5,
        ["1/8"] = 0.5,
        ["1/8."] = 0.75,
        ["1/16"] = 0.25,
    };

    private static readonly IReadOnlyList<EffectParameter> _eq =
    [
        Setting<EqEffect>("hpFreq", 20, 1000, e => e.HpFreq, (e, v) => e with { HpFreq = v }),
        Setting<EqEffect>("lowGain", -12, 12, e => e.LowGain, (e, v) => e with { LowGain = v }),
        Setting<EqEffect>("lowFreq", 40, 600, e => e.LowFreq, (e, v) => e with { LowFreq = v }),
        Setting<EqEffect>("midGain", -12, 12, e => e.MidGain, (e, v) => e with { MidGain = v }),
        Setting<EqEffect>("midFreq", 200, 8000, e => e.MidFreq, (e, v) => e with { MidFreq = v }),
        Setting<EqEffect>("midQ", 0.3, 4, e => e.MidQ, (e, v) => e with { MidQ = v }),
        Setting<EqEffect>("highGain", -12, 12, e => e.HighGain, (e, v) => e with { HighGain = v }),
        Setting<EqEffect>("highFreq", 1000, 16000, e => e.HighFreq, (e, v) => e with { HighFreq = v }),
        Setting<EqEffect>("lpFreq", 1000, 20000, e => e.LpFreq, (e, v) => e with { LpFreq = v }),
    ];

    private static readonly IReadOnlyList<EffectParameter> _compressor =
    [
        Setting<CompressorEffect>("threshold", -60, 0, e => e.Threshold, (e, v) => e with { Threshold = v }),
        Setting<CompressorEffect>("knee", 0, 40, e => e.Knee, (e, v) => e with { Knee = v }),
        Setting<CompressorEffect>("ratio", 1, 20, e => e.Ratio, (e, v) => e with { Ratio = v }),
        Setting<CompressorEffect>("attack", 0.001, 1, e => e.Attack, (e, v) => e with { Attack = v }),
        Setting<CompressorEffect>("release", 0.01, 1, e => e.Release, (e, v) => e with { Release = v }),
        Setting<CompressorEffect>("makeup", 0, 24, e => e.Makeup, (e, v) => e with { Makeup = v }),
    ];

    private static readonly IReadOnlyList<EffectParameter> _reverb =
    [
        Setting<ReverbEffect>("size", 0.1, 8, e => e.Size, (e, v) => e with { Size = v }),
        Setting<ReverbEffect>("decay", 0.5, 6, e => e.Decay, (e, v) => e with { Decay = v }),
        Setting<ReverbEffect>("predelay", 0, 0.2, e => e.Predelay, (e, v) => e with { Predelay = v }),
        Setting<ReverbEffect>("mix", 0, 1, e => e.Mix, (e, v) => e with { Mix = v }),
    ];

    private static readonly IReadOnlyList<EffectParameter> _delay =
    [
        Setting<DelayEffect>("time", 0.02, 2, e => e.Time, (e, v) => e with { Time = v }),
        Setting<DelayEffect>("feedback", 0, 0.9, e => e.Feedback, (e, v) => e with { Feedback = v }),
        Setting<DelayEffect>("tone", 500, 15000, e => e.Tone, (e, v) => e with { Tone = v }),
        Setting<DelayEffect>("mix", 0, 1, e => e.Mix, (e, v) => e with { Mix = v }),
    ];

    private static readonly IReadOnlyList<EffectParameter> _saturation =
    [
        Setting<SaturationEffect>("drive", 0, 1, e => e.Drive, (e, v) => e with { Drive = v }),
        Setting<SaturationEffect>("trim", -12, 6, e => e.Trim, (e, v) => e with { Trim = v }),
        Setting<SaturationEffect>("mix", 0, 1, e => e.Mix, (e, v) => e with { Mix = v }),
    ];

    /// <summary>The delay divisions a synced delay may use, longest first.</summary>
    public static IReadOnlyList<string> Divisions { get; } = [.. _divisions.Keys];

    /// <summary>The name of every numeric setting of every kind, once each.</summary>
    public static IReadOnlyList<string> AllParameterNames { get; } = [.. Enum.GetValues<EffectKind>().SelectMany(kind => Parameters(kind)).Select(setting => setting.Name).Distinct()];

    /// <summary>A new effect of a kind with the old DAW's starting settings, on.</summary>
    public static Effect Create(EffectKind kind, EffectId id)
    {
        return kind switch
        {
            EffectKind.Eq => new EqEffect
            {
                Id = id,
                Enabled = true,
                HpFreq = 20,
                LowGain = 0,
                LowFreq = 200,
                MidGain = 0,
                MidFreq = 1000,
                MidQ = 1,
                HighGain = 0,
                HighFreq = 4000,
                LpFreq = 20000,
            },
            EffectKind.Compressor => new CompressorEffect { Id = id, Enabled = true, Threshold = -24, Knee = 30, Ratio = 4, Attack = 0.003, Release = 0.25, Makeup = 0 },
            EffectKind.Reverb => new ReverbEffect { Id = id, Enabled = true, Size = 2, Decay = 3, Predelay = 0.02, Mix = 0.3 },
            EffectKind.Delay => new DelayEffect { Id = id, Enabled = true, Time = 0.35, Sync = false, Division = "1/4", Feedback = 0.35, Tone = 8000, Mix = 0.25 },
            _ => new SaturationEffect { Id = id, Enabled = true, Drive = 0.3, Trim = 0, Mix = 1 },
        };
    }

    /// <summary>The kind of an effect.</summary>
    public static EffectKind KindOf(Effect effect)
    {
        ArgumentNullException.ThrowIfNull(effect);
        return effect switch
        {
            EqEffect => EffectKind.Eq,
            CompressorEffect => EffectKind.Compressor,
            ReverbEffect => EffectKind.Reverb,
            DelayEffect => EffectKind.Delay,
            _ => EffectKind.Saturation,
        };
    }

    /// <summary>The numeric settings of a kind, with their ranges.</summary>
    public static IReadOnlyList<EffectParameter> Parameters(EffectKind kind)
    {
        return kind switch
        {
            EffectKind.Eq => _eq,
            EffectKind.Compressor => _compressor,
            EffectKind.Reverb => _reverb,
            EffectKind.Delay => _delay,
            _ => _saturation,
        };
    }

    /// <summary>The setting of an effect's kind with this name, or null.</summary>
    public static EffectParameter? Find(Effect effect, string name)
    {
        return Parameters(KindOf(effect)).FirstOrDefault(setting => setting.Name == name);
    }

    /// <summary>Whether <paramref name="division"/> is one a synced delay may use.</summary>
    public static bool IsDivision(string division) => _divisions.ContainsKey(division);

    /// <summary>The time of a delay that follows the tempo: its division at <paramref name="bpm"/>, at most four seconds.</summary>
    public static double SyncedDelayTime(string division, double bpm)
    {
        double beats = _divisions.TryGetValue(division, out double known) ? known : 1;
        return Math.Min(4, 60 / (bpm > 0 ? bpm : 120) * beats);
    }

    private static EffectParameter Setting<TEffect>(string name, double min, double max, Func<TEffect, double> get, Func<TEffect, double, TEffect> set)
        where TEffect : Effect
    {
        return new EffectParameter(name, min, max, effect => get((TEffect)effect), (effect, value) => set((TEffect)effect, value));
    }
}
