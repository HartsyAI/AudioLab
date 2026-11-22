using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>Copies an audio or MIDI clip onto the same track, over its clips; the server allocates the copy's id.</summary>
public sealed record DuplicateClip : Command
{
    /// <summary>The clip to copy.</summary>
    [Description("The clip to copy.")]
    public required ClipId Clip { get; init; }

    /// <summary>Where the copy starts, in seconds; half a second after the original ends when absent.</summary>
    [Description("Where the copy starts, in seconds; half a second after the original ends when absent.")]
    [Range(0, ProjectLimits.MaxSeconds)]
    public double? Start { get; init; }
}
