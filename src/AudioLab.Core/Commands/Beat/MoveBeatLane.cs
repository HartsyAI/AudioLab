using System.ComponentModel;
using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Core.Commands;

/// <summary>Moves a lane one place up or down in the drum pattern, which changes its pads' order; at either end nothing changes.</summary>
public sealed record MoveBeatLane : Command
{
    /// <summary>The drum lane to move.</summary>
    [Description("The drum lane to move.")]
    public required BeatLaneId Lane { get; init; }

    /// <summary>up is towards the first lane, down is towards the last.</summary>
    [Description("up is towards the first lane, down is towards the last.")]
    public required MoveDirection Direction { get; init; }
}
