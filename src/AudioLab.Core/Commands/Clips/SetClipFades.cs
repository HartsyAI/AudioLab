using System.ComponentModel;
using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>Sets the fade in and fade out of an audio clip; each is kept to what is left of the clip after the other.</summary>
public sealed record SetClipFades : Command
{
    /// <summary>The audio clip to edit.</summary>
    [Description("The audio clip to edit.")]
    public required ClipId Clip { get; init; }

    /// <summary>The fade in length, in seconds; absent to leave it.</summary>
    [Description("The fade in length, in seconds; absent to leave it.")]
    public double? FadeIn { get; init; }

    /// <summary>The fade out length, in seconds; absent to leave it.</summary>
    [Description("The fade out length, in seconds; absent to leave it.")]
    public double? FadeOut { get; init; }
}
