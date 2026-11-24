using AudioLab.Core.Model;

namespace AudioLab.Web.Components;

/// <summary>One effect of a channel strip's chain as the strip shows it.</summary>
/// <param name="Id">The effect's id, or null for what is shown in its place and is not an effect, such as the master limiter.</param>
/// <param name="Name">What the slot says.</param>
/// <param name="Enabled">Whether it is in the signal path.</param>
public readonly record struct StripEffect(EffectId? Id, string Name, bool Enabled);
