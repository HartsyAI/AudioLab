using System.ComponentModel;
using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>Removes a track with its clips; sends into a removed bus go with it, and a group left empty goes too.</summary>
public sealed record RemoveTrack : Command
{
    /// <summary>The track to remove.</summary>
    [Description("The track to remove.")]
    public required TrackId Track { get; init; }
}
