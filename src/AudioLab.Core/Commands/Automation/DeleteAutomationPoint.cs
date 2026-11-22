using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>Removes one point from a lane.</summary>
public sealed record DeleteAutomationPoint : Command
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
}
