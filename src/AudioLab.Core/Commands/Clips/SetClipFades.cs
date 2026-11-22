using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>Sets the fade in and fade out of an audio clip; the fade in is kept to the clip and the fade out to what it leaves.</summary>
public sealed record SetClipFades : Command
{
    /// <summary>The audio clip to edit.</summary>
    [Description("The audio clip to edit.")]
    public required ClipId Clip { get; init; }

    /// <summary>The fade in length, in seconds, from 0; absent to leave it.</summary>
    [Description("The fade in length, in seconds, from 0; absent to leave it.")]
    [Range(0, ProjectLimits.MaxSeconds)]
    public double? FadeIn { get; init; }

    /// <summary>The fade out length, in seconds, from 0; absent to leave it.</summary>
    [Description("The fade out length, in seconds, from 0; absent to leave it.")]
    [Range(0, ProjectLimits.MaxSeconds)]
    public double? FadeOut { get; init; }
}
