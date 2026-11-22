using System.ComponentModel;
using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>Removes a track with its clips; sends into a removed bus go with it, a group left empty goes too, and the score-parent and stem links other clips had to its clips are cleared.</summary>
public sealed record RemoveTrack : Command
{
    /// <summary>The track to remove.</summary>
    [Description("The track to remove.")]
    public required TrackId Track { get; init; }
}
