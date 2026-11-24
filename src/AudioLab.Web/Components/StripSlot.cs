using AudioLab.Core.Model;

namespace AudioLab.Web.Components;

/// <summary>One slot of a channel strip: what it says, whether it is off, and what pressing it opens.</summary>
/// <param name="Text">What the slot says, or null for an empty slot.</param>
/// <param name="Enabled">Whether the effect it names is in the signal path.</param>
/// <param name="Effect">The effect the effects tab shows when the slot is pressed, or null for the track's chain as it is.</param>
/// <param name="Opens">Whether pressing the slot does anything.</param>
/// <param name="Overflow">Whether the slot stands for several effects that do not fit, and is on when any of them is.</param>
public readonly record struct StripSlot(string? Text, bool Enabled, EffectId? Effect, bool Opens, bool Overflow = false);
