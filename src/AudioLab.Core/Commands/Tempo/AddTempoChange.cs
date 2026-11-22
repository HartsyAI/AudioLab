using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Core.Commands;

/// <summary>Adds a tempo change; the server allocates its id.</summary>
public sealed record AddTempoChange : Command
{
    /// <summary>Where it starts, in seconds; at least 0.01.</summary>
    [Description("Where it starts, in seconds; at least 0.01.")]
    [Range(TimelineRules.MinTempoChangeTime, ProjectLimits.MaxSeconds)]
    public required double Time { get; init; }

    /// <summary>The tempo from there on, in beats per minute, from 1 to 1000.</summary>
    [Description("The tempo from there on, in beats per minute, from 1 to 1000.")]
    [Range(ProjectLimits.MinBpm, ProjectLimits.MaxBpm)]
    public required double Bpm { get; init; }
}
