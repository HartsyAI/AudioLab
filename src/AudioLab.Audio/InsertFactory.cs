using AudioLab.Core.Model;

namespace AudioLab.Audio;

/// <summary>Builds the insert that runs an effect of the project.</summary>
internal static class InsertFactory
{
    /// <summary>An insert that only reports the tail and latency of an effect, or null when the effect is bypassed; it makes no sound.</summary>
    public static IInsert? CreateTailOnly(Effect effect, InsertContext context)
    {
        ArgumentNullException.ThrowIfNull(effect);
        ArgumentNullException.ThrowIfNull(context);
        if (!effect.Enabled)
        {
            return null;
        }
        return effect switch
        {
            ReverbEffect reverb => new TailOnlyInsert(ReverbInsert.TailFor(reverb, context.SampleRate), 0, false),
            DelayEffect delay => new TailOnlyInsert(
                DelayInsert.TailFor(delay, context),
                0,
                DelayInsert.UncutTailFor(delay, context) > DelayInsert.MaxTailSeconds),
            SaturationEffect => new TailOnlyInsert((double)SaturationInsert.Latency / context.SampleRate, SaturationInsert.Latency, false),
            _ => new TailOnlyInsert(0, 0, false),
        };
    }

    /// <summary>The insert for an effect, or null when the effect is bypassed.</summary>
    public static IInsert? Create(Effect effect, InsertContext context)
    {
        ArgumentNullException.ThrowIfNull(effect);
        ArgumentNullException.ThrowIfNull(context);
        if (!effect.Enabled)
        {
            return null;
        }
        return effect switch
        {
            EqEffect eq => new EqInsert(eq, context.SampleRate),
            CompressorEffect compressor => new CompressorInsert(compressor, context.SampleRate),
            ReverbEffect reverb => new ReverbInsert(reverb, context.SampleRate),
            DelayEffect delay => new DelayInsert(delay, context),
            SaturationEffect saturation => new SaturationInsert(saturation, context.SampleRate),
            _ => throw new NotSupportedException($"The effect {effect.GetType().Name} has no insert."),
        };
    }
}
