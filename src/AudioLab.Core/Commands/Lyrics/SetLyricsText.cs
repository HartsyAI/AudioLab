using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>Sets the text of a clip's lyrics; with no words in it the lyrics are removed.</summary>
public sealed record SetLyricsText : Command
{
    /// <summary>The audio clip whose lyrics are edited.</summary>
    [Description("The audio clip whose lyrics are edited.")]
    public required ClipId Clip { get; init; }

    /// <summary>The lyrics, up to 1000000 characters.</summary>
    /// <remarks>
    /// section tags such as [Verse 1] are allowed. Word times stay when only spacing, case, punctuation or tags
    /// changed.
    /// </remarks>
    [Description(
        "The lyrics, up to 1000000 characters; section tags such as [Verse 1] are allowed. Word times stay " +
        "when only spacing, case, punctuation or tags changed.")]
    [MaxLength(ProjectLimits.MaxLongText)]
    public required string Text { get; init; }
}
