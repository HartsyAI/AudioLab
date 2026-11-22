using System.ComponentModel;
using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>Arms a track to record on the transport's record, or disarms it; an aux bus has no input to arm.</summary>
public sealed record SetTrackArmed : Command
{
    /// <summary>The track to edit.</summary>
    [Description("The track to edit.")]
    public required TrackId Track { get; init; }

    /// <summary>True to record this track.</summary>
    [Description("True to record this track.")]
    public required bool Armed { get; init; }
}
