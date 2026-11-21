using System.Text.Json.Serialization;

namespace AudioLab.Core.Model;

/// <summary>Which channels of a track's input device are recorded.</summary>
[JsonConverter(typeof(StrictEnumConverter<InputChannelMode>))]
public enum InputChannelMode
{
    /// <summary>The inputs summed to one channel.</summary>
    [JsonStringEnumMemberName("mono")]
    Mono,

    /// <summary>The first two inputs.</summary>
    [JsonStringEnumMemberName("stereo")]
    Stereo,

    /// <summary>One numbered input of a multi-input interface.</summary>
    [JsonStringEnumMemberName("single")]
    Single,
}
