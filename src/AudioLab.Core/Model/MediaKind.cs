using System.Text.Json.Serialization;

namespace AudioLab.Core.Model;

/// <summary>What a media file holds.</summary>
[JsonConverter(typeof(StrictEnumConverter<MediaKind>))]
public enum MediaKind
{
    /// <summary>Recorded or generated sound: a clip's source, a drum pad's sample or a sampler's sample.</summary>
    [JsonStringEnumMemberName("audio")]
    Audio,
}
