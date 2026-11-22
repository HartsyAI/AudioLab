using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>Sets the start or the end of one word, kept inside its neighbours; the word stops being flagged unsure.</summary>
public sealed record SetLyricsWordTime : Command
{
    /// <summary>The audio clip whose lyrics are edited.</summary>
    [Description("The audio clip whose lyrics are edited.")]
    public required ClipId Clip { get; init; }

    /// <summary>The word's place in the lyrics, counted from 0.</summary>
    [Description("The word's place in the lyrics, counted from 0.")]
    [Range(0, ProjectLimits.MaxListItems - 1)]
    public required int Word { get; init; }

    /// <summary>Where the word starts, in seconds of the clip's source audio; it keeps its start when absent.</summary>
    [Description("Where the word starts, in seconds of the clip's source audio; it keeps its start when absent.")]
    [Range(0, ProjectLimits.MaxSeconds)]
    public double? Start { get; init; }

    /// <summary>Where the word ends, in seconds of the clip's source audio; it keeps its end when absent.</summary>
    [Description("Where the word ends, in seconds of the clip's source audio; it keeps its end when absent.")]
    [Range(0, ProjectLimits.MaxSeconds)]
    public double? End { get; init; }
}
