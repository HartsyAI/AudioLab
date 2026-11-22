using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>Renames the project.</summary>
public sealed record RenameProject : Command
{
    /// <summary>The new name, 1 to 200 characters, not blank.</summary>
    [Description("The new name, 1 to 200 characters, not blank.")]
    [MinLength(1)]
    [MaxLength(ProjectLimits.MaxName)]
    public required string Name { get; init; }
}
