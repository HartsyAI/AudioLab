using System.ComponentModel;
using System.Text.Json.Serialization;
using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>Sets the sample a drum lane plays.</summary>
public sealed record SetBeatLaneMedia : Command
{
    /// <summary>The drum lane to edit.</summary>
    [Description("The drum lane to edit.")]
    public required BeatLaneId Lane { get; init; }

    /// <summary>The sample the lane plays; absent to leave the lane without one.</summary>
    [Description("The sample the lane plays; absent to leave the lane without one.")]
    [JsonConverter(typeof(OptionalIdJsonConverter<MediaId>))]
    public MediaId? Media { get; init; }
}
