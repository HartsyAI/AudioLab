using System.Text.Json.Serialization;
using AudioLab.Core.Model;

namespace AudioLab.Core.Playback;

/// <summary>Whether the engine is playing, and where a stop puts the playhead.</summary>
[JsonConverter(typeof(StrictEnumConverter<TransportState>))]
public enum TransportState
{
    /// <summary>Not playing; the playhead is where playing last began, or where it was moved.</summary>
    [JsonStringEnumMemberName("stopped")]
    Stopped,

    /// <summary>Playing from the playhead.</summary>
    [JsonStringEnumMemberName("playing")]
    Playing,

    /// <summary>Not playing; the playhead stays where playing ended.</summary>
    [JsonStringEnumMemberName("paused")]
    Paused,
}
