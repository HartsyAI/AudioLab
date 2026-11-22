using System.ComponentModel;
using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>Removes a lane and its steps from the drum pattern.</summary>
public sealed record RemoveBeatLane : Command
{
    /// <summary>The drum lane to edit.</summary>
    [Description("The drum lane to edit.")]
    public required BeatLaneId Lane { get; init; }
}
