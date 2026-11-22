using System.Text.Json.Serialization;
using AudioLab.Core.Model;

namespace AudioLab.Core.Rules;

/// <summary>Which way a track or group steps in the track list.</summary>
[JsonConverter(typeof(StrictEnumConverter<MoveDirection>))]
public enum MoveDirection
{
    /// <summary>Towards the top of the list.</summary>
    [JsonStringEnumMemberName("up")]
    Up,

    /// <summary>Towards the bottom of the list.</summary>
    [JsonStringEnumMemberName("down")]
    Down,
}
