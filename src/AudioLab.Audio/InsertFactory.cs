using AudioLab.Core.Model;

namespace AudioLab.Audio;

/// <summary>Builds the insert that runs an effect of the project.</summary>
internal static class InsertFactory
{
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
            SaturationEffect saturation => new SaturationInsert(saturation),
            _ => throw new NotSupportedException($"The effect {effect.GetType().Name} has no insert."),
        };
    }
}
