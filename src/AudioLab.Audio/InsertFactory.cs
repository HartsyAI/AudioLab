using AudioLab.Core.Model;
using AudioLab.Core.Rules;

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
        // Refused, not skipped: a render that left out an enabled effect would be silently wrong.
        throw new NotSupportedException($"The {EffectRules.KindOf(effect)} effect is not rendered yet.");
    }
}
