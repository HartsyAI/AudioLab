using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>Sets the version label of a clip's score; a clip with no score is refused.</summary>
public sealed record SetScoreLabel : Command
{
    /// <summary>The audio clip to edit.</summary>
    [Description("The audio clip to edit.")]
    public required ClipId Clip { get; init; }

    /// <summary>The label, up to 200 characters; absent removes it.</summary>
    [Description("The label, up to 200 characters; absent removes it.")]
    [MaxLength(ProjectLimits.MaxName)]
    public string? Label { get; init; }
}
