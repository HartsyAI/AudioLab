using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Core.Commands;

/// <summary>Puts a point at a new time and value, kept in time order; the target is absolute, so a drag can come back.</summary>
public sealed record MoveAutomationPoint : Command
{
    /// <summary>The track whose lane is edited.</summary>
    [Description("The track whose lane is edited.")]
    public required TrackId Track { get; init; }

    /// <summary>The lane: volume or pan.</summary>
    [Description("The lane: volume or pan.")]
    public required AutomationParameter Parameter { get; init; }

    /// <summary>The point's place in the lane, counted from 0 by time, before the command runs.</summary>
    [Description("The point's place in the lane, counted from 0 by time, before the command runs.")]
    [Range(0, ProjectLimits.MaxListItems - 1)]
    public required int Index { get; init; }

    /// <summary>Where the point sits, in seconds from the start of the timeline.</summary>
    [Description("Where the point sits, in seconds from the start of the timeline.")]
    [Range(0, ProjectLimits.MaxSeconds)]
    public required double Time { get; init; }

    /// <summary>The new value: kept inside the lane, volume 0 to 16, pan -1 to 1.</summary>
    [Description("The new value: kept inside the lane, volume 0 to 16, pan -1 to 1.")]
    [Range(-1, AutomationRules.VolumeMax)]
    public required double Value { get; init; }
}
