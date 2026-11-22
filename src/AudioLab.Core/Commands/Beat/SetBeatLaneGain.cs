using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>Sets the gain of a drum lane.</summary>
public sealed record SetBeatLaneGain : Command
{
    /// <summary>The drum lane to edit.</summary>
    [Description("The drum lane to edit.")]
    public required BeatLaneId Lane { get; init; }

    /// <summary>The lane gain from 0 up to 16; 1 is unity.</summary>
    [Description("The lane gain from 0 up to 16; 1 is unity.")]
    [Range(0, ProjectLimits.MaxGain)]
    public required double Gain { get; init; }
}
