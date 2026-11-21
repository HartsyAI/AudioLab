using System.Text.Json.Serialization;

namespace AudioLab.Core.Model;

/// <summary>The parameter whose lane a track shows.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<AutomationParameter>))]
public enum AutomationParameter
{
    /// <summary>The track volume.</summary>
    [JsonStringEnumMemberName("volume")]
    Volume,

    /// <summary>The track pan.</summary>
    [JsonStringEnumMemberName("pan")]
    Pan,
}
