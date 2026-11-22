using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>Switches steps of a drum lane on or off; the others stay as they are.</summary>
public sealed record SetBeatLaneSteps : Command
{
    /// <summary>The drum lane to edit.</summary>
    [Description("The drum lane to edit.")]
    public required BeatLaneId Lane { get; init; }

    /// <summary>The steps to set, counted from 0 and below the pattern's step count, from 1 to 256 of them.</summary>
    [Description("The steps to set, counted from 0 and below the pattern's step count, from 1 to 256 of them.")]
    [JsonConverter(typeof(ValueListJsonConverter<int>))]
    [MinLength(1)]
    [MaxLength(ProjectLimits.MaxBeatSteps)]
    [Range(0, ProjectLimits.MaxBeatSteps - 1)]
    public required ValueList<int> Steps { get; init; }

    /// <summary>True to make the steps fire, false to silence them.</summary>
    [Description("True to make the steps fire, false to silence them.")]
    public required bool On { get; init; }
}
