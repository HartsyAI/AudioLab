using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>Moves every word of a clip's lyrics earlier or later, never before 0.</summary>
public sealed record ShiftLyrics : Command
{
    /// <summary>The audio clip whose lyrics are edited.</summary>
    [Description("The audio clip whose lyrics are edited.")]
    public required ClipId Clip { get; init; }

    /// <summary>How far to move the words, in seconds; negative is earlier.</summary>
    [Description("How far to move the words, in seconds; negative is earlier.")]
    [Range(-ProjectLimits.MaxSeconds, ProjectLimits.MaxSeconds)]
    public required double Seconds { get; init; }
}
