using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>Renames a track.</summary>
public sealed record RenameTrack : Command
{
    /// <summary>The track to rename.</summary>
    [Description("The track to rename.")]
    public required TrackId Track { get; init; }

    /// <summary>The new name, 1 to 200 characters, not blank.</summary>
    [Description("The new name, 1 to 200 characters, not blank.")]
    [MinLength(1)]
    [MaxLength(ProjectLimits.MaxName)]
    public required string Name { get; init; }
}
