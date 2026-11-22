using System.ComponentModel;
using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>Solos or unsolos a track.</summary>
public sealed record SetTrackSoloed : Command
{
    /// <summary>The track to edit.</summary>
    [Description("The track to edit.")]
    public required TrackId Track { get; init; }

    /// <summary>True to solo the track.</summary>
    [Description("True to solo the track.")]
    public required bool Soloed { get; init; }
}
