using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>Marks an audio clip as a stem separated from another; a vocal stem takes the source's lyrics.</summary>
public sealed record SetClipStem : Command
{
    /// <summary>The audio clip to edit.</summary>
    [Description("The audio clip to edit.")]
    public required ClipId Clip { get; init; }

    /// <summary>The audio clip the stem was separated from.</summary>
    [Description("The audio clip the stem was separated from.")]
    public required ClipId Source { get; init; }

    /// <summary>The stem name, such as vocals or drums, not blank and up to 128 characters.</summary>
    [Description("The stem name, such as vocals or drums, not blank and up to 128 characters.")]
    [MaxLength(ProjectLimits.MaxLabel)]
    public required string Stem { get; init; }

    /// <summary>Where in the source file the stem's audio began when it was cut, in seconds, from 0 to the source's.</summary>
    /// <remarks>
    /// length; the source's current offset when absent.
    /// </remarks>
    [Description(
        "Where in the source file the stem's audio began when it was cut, in seconds, from 0 to the source's " +
        "length; the source's current offset when absent.")]
    [Range(0, ProjectLimits.MaxSeconds)]
    public double? SourceOffset { get; init; }
}
