using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>Sets the icon of a track, or removes it.</summary>
public sealed record SetTrackIcon : Command
{
    /// <summary>The track to edit.</summary>
    [Description("The track to edit.")]
    public required TrackId Track { get; init; }

    /// <summary>A short text such as an emoji (up to 16 characters) or a png, jpeg or webp data URL of up to 60000.</summary>
    /// <remarks>
    /// characters; absent removes it.
    /// </remarks>
    [Description(
        "A short text such as an emoji (up to 16 characters) or a png, jpeg or webp data URL of up to 60000 " +
        "characters; absent removes it.")]
    [MaxLength(TrackIcon.MaxImageLength)]
    public string? Icon { get; init; }
}
