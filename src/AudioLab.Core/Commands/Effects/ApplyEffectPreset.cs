using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>Replaces an effect's settings with a preset's laid over the kind's defaults; its place and bypass stay.</summary>
public sealed record ApplyEffectPreset : Command
{
    /// <summary>The track that holds the effect.</summary>
    [Description("The track that holds the effect.")]
    public required TrackId Track { get; init; }

    /// <summary>The effect to change.</summary>
    [Description("The effect to change.")]
    public required EffectId Effect { get; init; }

    /// <summary>The preset's name; it must be one of the effect's kind.</summary>
    [Description("The preset's name; it must be one of the effect's kind.")]
    [AllowedValues(
        "Vocal clarity", "Drum punch", "Bass focus", "Telephone", "Vocal level", "Drum smash",
        "Glue (gentle)", "Small room", "Hall", "Cathedral", "Slap", "Quarter echo", "Dotted 8th", "Warm",
        "Crunch", "Destroy")]
    public required string Preset { get; init; }
}
