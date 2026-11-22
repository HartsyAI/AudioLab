using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>Puts points at new values and leaves their times; dragging a line segment sets its two end points.</summary>
public sealed record SetAutomationValues : Command
{
    /// <summary>The track whose lane is edited.</summary>
    [Description("The track whose lane is edited.")]
    public required TrackId Track { get; init; }

    /// <summary>The lane: volume or pan.</summary>
    [Description("The lane: volume or pan.")]
    public required AutomationParameter Parameter { get; init; }

    /// <summary>The points and their values, from 1 to 1000000; each value is kept inside the lane.</summary>
    [Description("The points and their values, from 1 to 1000000; each value is kept inside the lane.")]
    [JsonConverter(typeof(ValueListJsonConverter<PointValue>))]
    [MinLength(1)]
    [MaxLength(ProjectLimits.MaxListItems)]
    public required ValueList<PointValue> Points { get; init; }
}
