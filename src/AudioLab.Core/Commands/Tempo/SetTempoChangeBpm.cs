using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>Sets the tempo of one tempo change.</summary>
public sealed record SetTempoChangeBpm : Command
{
    /// <summary>The tempo change to edit.</summary>
    [Description("The tempo change to edit.")]
    public required TempoChangeId Change { get; init; }

    /// <summary>The tempo from its start on, in beats per minute, from 1 to 1000.</summary>
    [Description("The tempo from its start on, in beats per minute, from 1 to 1000.")]
    [Range(ProjectLimits.MinBpm, ProjectLimits.MaxBpm)]
    public required double Bpm { get; init; }
}
