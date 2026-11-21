using System.Text.Json.Serialization;

namespace AudioLab.Core.Model;

/// <summary>Which time ruler the arrangement shows first.</summary>
[JsonConverter(typeof(StrictEnumConverter<RulerMode>))]
public enum RulerMode
{
    /// <summary>Minutes and seconds.</summary>
    [JsonStringEnumMemberName("time")]
    Time,

    /// <summary>Bars and beats.</summary>
    [JsonStringEnumMemberName("beats")]
    Beats,
}
