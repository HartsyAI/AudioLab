using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>Sets the master fader.</summary>
public sealed record SetMasterVolume : Command
{
    /// <summary>The gain, from 0 to 16; 1 is unity.</summary>
    [Description("The gain, from 0 to 16; 1 is unity.")]
    [Range(0, ProjectLimits.MaxGain)]
    public required double Volume { get; init; }
}
