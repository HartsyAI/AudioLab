using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>Makes an audio clip play another media file in the same place; the lyrics go, since their times belong to the old file.</summary>
public sealed record ReplaceClipMedia : Command
{
    /// <summary>The audio clip to change.</summary>
    [Description("The audio clip to change.")]
    public required ClipId Clip { get; init; }

    /// <summary>The media file the clip plays from now on.</summary>
    [Description("The media file the clip plays from now on.")]
    public required MediaId Media { get; init; }

    /// <summary>The length of the whole new media file, in seconds, above 0.</summary>
    [Description("The length of the whole new media file, in seconds, above 0.")]
    [Range(ProjectLimits.Tolerance, ProjectLimits.MaxSeconds)]
    public required double Duration { get; init; }
}
