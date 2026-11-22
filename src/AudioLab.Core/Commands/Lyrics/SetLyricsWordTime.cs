using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>Sets the start, the end or both of one word, kept between the end of the word before and the start of the word after.</summary>
public sealed record SetLyricsWordTime : Command
{
    /// <summary>The audio clip whose lyrics are edited.</summary>
    [Description("The audio clip whose lyrics are edited.")]
    public required ClipId Clip { get; init; }

    /// <summary>The word's place in the lyrics, counted from 0.</summary>
    [Description("The word's place in the lyrics, counted from 0.")]
    [Range(0, ProjectLimits.MaxListItems - 1)]
    public required int Word { get; init; }

    /// <summary>Where the word starts, in seconds of the clip's source audio.</summary>
    /// <remarks>
    /// kept between its neighbours, and it keeps its start when absent.
    /// </remarks>
    [Description(
        "Where the word starts, in seconds of the clip's source audio; kept between its neighbours, and it " +
        "keeps its start when absent.")]
    [Range(0, ProjectLimits.MaxSeconds)]
    public double? Start { get; init; }

    /// <summary>Where the word ends, in seconds of the clip's source audio.</summary>
    /// <remarks>
    /// kept between its neighbours and no earlier than its start, and it keeps its end when absent. Give start,
    /// end or both; the word stops being flagged unsure.
    /// </remarks>
    [Description(
        "Where the word ends, in seconds of the clip's source audio; kept between its neighbours and no " +
        "earlier than its start, and it keeps its end when absent. Give start, end or both; the word stops " +
        "being flagged unsure.")]
    [Range(0, ProjectLimits.MaxSeconds)]
    public double? End { get; init; }
}
