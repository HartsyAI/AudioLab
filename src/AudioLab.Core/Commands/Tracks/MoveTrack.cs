using System.ComponentModel;
using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Core.Commands;

/// <summary>Moves a track one step: inside its group it swaps with its neighbour, outside it steps over the next track or whole group; at the end nothing changes.</summary>
public sealed record MoveTrack : Command
{
    /// <summary>The track to move.</summary>
    [Description("The track to move.")]
    public required TrackId Track { get; init; }

    /// <summary>Up or down the track list.</summary>
    [Description("Up or down the track list.")]
    public required MoveDirection Direction { get; init; }
}
