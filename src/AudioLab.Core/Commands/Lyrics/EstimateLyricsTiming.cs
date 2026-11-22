using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>Spreads the lyrics evenly over the visible part of a clip, roughly and without a model.</summary>
public sealed record EstimateLyricsTiming : Command
{
    /// <summary>The audio clip whose lyrics are edited.</summary>
    [Description("The audio clip whose lyrics are edited.")]
    public required ClipId Clip { get; init; }

    /// <summary>The lyrics to spread, which also become the clip's text; the clip's own text when absent.</summary>
    [Description("The lyrics to spread, which also become the clip's text; the clip's own text when absent.")]
    [MaxLength(ProjectLimits.MaxLongText)]
    public string? Text { get; init; }
}
