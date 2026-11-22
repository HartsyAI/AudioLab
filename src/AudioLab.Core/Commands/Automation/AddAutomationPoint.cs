using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Core.Commands;

/// <summary>Adds a point to a track's volume or pan lane; among points at the same time it goes last.</summary>
public sealed record AddAutomationPoint : Command
{
    /// <summary>The track whose lane is edited.</summary>
    [Description("The track whose lane is edited.")]
    public required TrackId Track { get; init; }

    /// <summary>The lane: volume or pan.</summary>
    [Description("The lane: volume or pan.")]
    public required AutomationParameter Parameter { get; init; }

    /// <summary>Where the point sits, in seconds from the start of the timeline.</summary>
    [Description("Where the point sits, in seconds from the start of the timeline.")]
    [Range(0, ProjectLimits.MaxSeconds)]
    public required double Time { get; init; }

    /// <summary>The value there: volume 0 to 2, pan -1 to 1.</summary>
    [Description("The value there: volume 0 to 2, pan -1 to 1.")]
    [Range(-1, AutomationRules.VolumeMax)]
    public required double Value { get; init; }
}
