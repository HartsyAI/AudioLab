using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Core.Commands;

/// <summary>A point of a lane and the value it takes.</summary>
/// <param name="Index">The point's place in the lane, counted from 0 by time, before the command runs.</param>
/// <param name="Value">Its new value, kept inside the lane: volume 0 to 16, pan -1 to 1.</param>
public readonly record struct PointValue(
    [property: Description("The point's place in the lane, counted from 0 by time, before the command runs.")]
    [property: Range(0, ProjectLimits.MaxListItems - 1)]
    int Index,
    [property: Description("Its new value, kept inside the lane: volume 0 to 16, pan -1 to 1.")]
    [property: Range(-1, AutomationRules.VolumeMax)]
    double Value);
