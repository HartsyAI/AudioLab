using System.Text.Json.Serialization;
using AudioLab.Core.Model;

namespace AudioLab.Core.Rules;

/// <summary>Where a clip sits among the clips of its track; a later clip plays over an earlier one.</summary>
[JsonConverter(typeof(StrictEnumConverter<ClipLayer>))]
public enum ClipLayer
{
    /// <summary>Over every other clip of the track, so it is the take that plays.</summary>
    [JsonStringEnumMemberName("front")]
    Front,

    /// <summary>Under every other clip of the track.</summary>
    [JsonStringEnumMemberName("back")]
    Back,
}
