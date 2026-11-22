using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace AudioLab.Core.Commands;

/// <summary>Sets how far odd steps are pushed late.</summary>
public sealed record SetBeatSwing : Command
{
    /// <summary>The swing from 0 (none) to 1 (a whole step late); the old DAW offered up to 0.6.</summary>
    [Description("The swing from 0 (none) to 1 (a whole step late); the old DAW offered up to 0.6.")]
    [Range(0, 1)]
    public required double Swing { get; init; }
}
