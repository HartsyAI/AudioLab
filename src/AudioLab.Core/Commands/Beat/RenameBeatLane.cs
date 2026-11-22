using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>Renames a drum lane.</summary>
public sealed record RenameBeatLane : Command
{
    /// <summary>The drum lane to edit.</summary>
    [Description("The drum lane to edit.")]
    public required BeatLaneId Lane { get; init; }

    /// <summary>The new name, not blank, up to 200 characters.</summary>
    [Description("The new name, not blank, up to 200 characters.")]
    [MaxLength(ProjectLimits.MaxName)]
    public required string Name { get; init; }
}
