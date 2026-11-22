using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>Sets a track's fader.</summary>
public sealed record SetTrackVolume : Command
{
    /// <summary>The track to edit.</summary>
    [Description("The track to edit.")]
    public required TrackId Track { get; init; }

    /// <summary>The gain, from 0 to 16; 1 is unity.</summary>
    [Description("The gain, from 0 to 16; 1 is unity.")]
    [Range(0, ProjectLimits.MaxGain)]
    public required double Volume { get; init; }
}
