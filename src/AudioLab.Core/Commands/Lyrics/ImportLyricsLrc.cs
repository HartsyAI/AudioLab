using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>Gives a clip the lyrics of an LRC file, each line timed by estimate from its own start.</summary>
public sealed record ImportLyricsLrc : Command
{
    /// <summary>The audio clip whose lyrics are edited.</summary>
    [Description("The audio clip whose lyrics are edited.")]
    public required ClipId Clip { get; init; }

    /// <summary>The file's text, up to 1000000 characters.</summary>
    [Description("The file's text, up to 1000000 characters.")]
    [MaxLength(ProjectLimits.MaxLongText)]
    public required string Lrc { get; init; }

    /// <summary>How long the last line lasts, in seconds; 4 when absent.</summary>
    [Description("How long the last line lasts, in seconds; 4 when absent.")]
    [Range(0, ProjectLimits.MaxSeconds)]
    public double? LastLine { get; init; }
}
