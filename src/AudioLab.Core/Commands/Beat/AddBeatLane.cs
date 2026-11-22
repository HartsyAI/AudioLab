using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>Adds a lane to the drum pattern at the bottom; the server allocates the lane id.</summary>
public sealed record AddBeatLane : Command
{
    /// <summary>The lane's name, up to 200 characters; "Pad N" when absent or blank.</summary>
    [Description("The lane's name, up to 200 characters; \"Pad N\" when absent or blank.")]
    [MaxLength(ProjectLimits.MaxName)]
    public string? Name { get; init; }

    /// <summary>The sample the lane plays; the lane has none while it is absent.</summary>
    [Description("The sample the lane plays; the lane has none while it is absent.")]
    [JsonConverter(typeof(OptionalIdJsonConverter<MediaId>))]
    public MediaId? Media { get; init; }
}
