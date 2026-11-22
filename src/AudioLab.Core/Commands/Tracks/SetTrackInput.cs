using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>Sets where a track records from: the device and which of its channels.</summary>
public sealed record SetTrackInput : Command
{
    /// <summary>The track to edit.</summary>
    [Description("The track to edit.")]
    public required TrackId Track { get; init; }

    /// <summary>The input device, up to 512 characters; the system default when absent.</summary>
    [Description("The input device, up to 512 characters; the system default when absent.")]
    [MaxLength(512)]
    public string? DeviceId { get; init; }

    /// <summary>Which channels are recorded.</summary>
    /// <remarks>
    /// mono (the inputs summed), stereo (the first two) or single (one numbered input).
    /// </remarks>
    [Description(
        "Which channels are recorded: mono (the inputs summed), stereo (the first two) or single (one " +
        "numbered input).")]
    public required InputChannelMode Channels { get; init; }

    /// <summary>The 1-based input when Channels is single, 1 to 256; it must be absent for the other modes.</summary>
    [Description("The 1-based input when Channels is single, 1 to 256; it must be absent for the other modes.")]
    [Range(1, 256)]
    public int? Channel { get; init; }
}
