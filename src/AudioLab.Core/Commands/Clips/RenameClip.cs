using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>Renames an audio or MIDI clip.</summary>
public sealed record RenameClip : Command
{
    /// <summary>The clip to rename.</summary>
    [Description("The clip to rename.")]
    public required ClipId Clip { get; init; }

    /// <summary>The new name, 1 to 200 characters, not blank.</summary>
    [Description("The new name, 1 to 200 characters, not blank.")]
    [MinLength(1)]
    [MaxLength(ProjectLimits.MaxName)]
    public required string Name { get; init; }
}
