using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>Sets whether a delay follows the tempo and which note division it uses.</summary>
public sealed record SetDelaySync : Command
{
    /// <summary>The track that holds the delay.</summary>
    [Description("The track that holds the delay.")]
    public required TrackId Track { get; init; }

    /// <summary>The delay to edit.</summary>
    [Description("The delay to edit.")]
    public required EffectId Effect { get; init; }

    /// <summary>True to follow the project tempo with the division; false to use its time setting.</summary>
    [Description("True to follow the project tempo with the division; false to use its time setting.")]
    public required bool Sync { get; init; }

    /// <summary>The note division: 1/2, 1/4, 1/4., 1/8, 1/8. or 1/16; it keeps its value when absent.</summary>
    [Description("The note division: 1/2, 1/4, 1/4., 1/8, 1/8. or 1/16; it keeps its value when absent.")]
    [AllowedValues("1/2", "1/4", "1/4.", "1/8", "1/8.", "1/16")]
    public string? Division { get; init; }
}
