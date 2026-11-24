using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Web.Session.Effects;

/// <summary>When a setting of an effect cannot be changed and why, so a knob that does nothing always says so.</summary>
public static class EffectSettingRules
{
    /// <summary>What a delay's time knob says while the delay follows the tempo.</summary>
    public const string FollowsTempo = "The time follows the tempo while Sync is on.";

    /// <summary>Why a setting cannot be changed now, or null when it can.</summary>
    /// <param name="effect">The effect that holds the setting.</param>
    /// <param name="setting">The setting.</param>
    /// <param name="editable">Whether the project can be edited from here.</param>
    /// <param name="reason">Why it cannot, as the screen says it.</param>
    public static string? Block(Effect effect, EffectParameter setting, bool editable, string? reason)
    {
        ArgumentNullException.ThrowIfNull(effect);
        ArgumentNullException.ThrowIfNull(setting);
        if (!editable)
        {
            return reason ?? "Not available.";
        }
        return effect is DelayEffect { Sync: true } && setting.FollowsTempo ? FollowsTempo : null;
    }
}
