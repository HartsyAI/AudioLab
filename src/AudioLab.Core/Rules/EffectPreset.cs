namespace AudioLab.Core.Rules;

/// <summary>A starter sound for one kind of effect: values laid over the kind's defaults.</summary>
/// <param name="Name">The preset's name, as a menu shows it.</param>
/// <param name="Values">The numeric settings the preset sets, by parameter name; every other setting returns to its default.</param>
/// <param name="Sync">For a delay, whether it follows the tempo; null leaves the default.</param>
/// <param name="Division">For a synced delay, the note division; null leaves the default.</param>
public sealed record EffectPreset(string Name, IReadOnlyDictionary<string, double> Values, bool? Sync = null, string? Division = null);
