using System.Text.Json.Serialization;

namespace AudioLab.Core.Model;

/// <summary>How a track's automation lanes behave during playback.</summary>
[JsonConverter(typeof(StrictEnumConverter<AutomationMode>))]
public enum AutomationMode
{
    /// <summary>Plays the lanes back.</summary>
    [JsonStringEnumMemberName("read")]
    Read,

    /// <summary>Records while a control is held.</summary>
    [JsonStringEnumMemberName("touch")]
    Touch,

    /// <summary>Records from the first touch to the stop.</summary>
    [JsonStringEnumMemberName("latch")]
    Latch,

    /// <summary>Records for the whole pass.</summary>
    [JsonStringEnumMemberName("write")]
    Write,

    /// <summary>Ignores the lanes.</summary>
    [JsonStringEnumMemberName("off")]
    Off,
}
