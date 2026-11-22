using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>Folds a recorded pass into a lane: the written points replace what was under them.</summary>
public sealed record CommitAutomationWrite : Command
{
    /// <summary>The track whose lane is edited.</summary>
    [Description("The track whose lane is edited.")]
    public required TrackId Track { get; init; }

    /// <summary>The lane: volume or pan.</summary>
    [Description("The lane: volume or pan.")]
    public required AutomationParameter Parameter { get; init; }

    /// <summary>The points the pass wrote, from 1 to 1000000, in any order.</summary>
    [Description("The points the pass wrote, from 1 to 1000000, in any order.")]
    [JsonConverter(typeof(ValueListJsonConverter<WrittenPoint>))]
    [MinLength(1)]
    [MaxLength(ProjectLimits.MaxListItems)]
    public required ValueList<WrittenPoint> Points { get; init; }

    /// <summary>True to hand back to the old curve just after the pass, as Touch does.</summary>
    /// <remarks>
    /// false to keep the old points after the pass, so the lane ramps from the last written value to the next
    /// old point, as Latch and Write do.
    /// </remarks>
    [Description(
        "True to hand back to the old curve just after the pass, as Touch does; false to keep the old points " +
        "after the pass, so the lane ramps from the last written value to the next old point, as Latch and " +
        "Write do.")]
    public required bool ReturnToOld { get; init; }
}
