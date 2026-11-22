using System.ComponentModel;
using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>Mutes or unmutes a track.</summary>
public sealed record SetTrackMuted : Command
{
    /// <summary>The track to edit.</summary>
    [Description("The track to edit.")]
    public required TrackId Track { get; init; }

    /// <summary>True to silence the track.</summary>
    [Description("True to silence the track.")]
    public required bool Muted { get; init; }
}
