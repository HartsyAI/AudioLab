using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>Sets the media file a sampler plays, or leaves it with none.</summary>
public sealed record SetSamplerSample : Command
{
    /// <summary>The instrument track to edit.</summary>
    [Description("The instrument track to edit.")]
    public required TrackId Track { get; init; }

    /// <summary>The media file to play; the sampler has none when absent.</summary>
    [Description("The media file to play; the sampler has none when absent.")]
    [JsonConverter(typeof(OptionalIdJsonConverter<MediaId>))]
    public MediaId? Sample { get; init; }

    /// <summary>The name shown for the sample, up to 200 characters; empty when absent.</summary>
    [Description("The name shown for the sample, up to 200 characters; empty when absent.")]
    [MaxLength(ProjectLimits.MaxName)]
    public string? SampleName { get; init; }
}
