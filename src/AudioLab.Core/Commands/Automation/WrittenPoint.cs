using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Core.Commands;

/// <summary>A point a recorded pass wrote.</summary>
/// <param name="Time">Where it was written, in seconds from the start of the timeline.</param>
/// <param name="Value">The value written: volume 0 to 2, pan -1 to 1.</param>
public readonly record struct WrittenPoint(
    [property: Description("Where it was written, in seconds from the start of the timeline.")]
    [property: Range(0, ProjectLimits.MaxSeconds)]
    double Time,
    [property: Description("The value written: volume 0 to 2, pan -1 to 1.")]
    [property: Range(-1, AutomationRules.VolumeMax)]
    double Value);
