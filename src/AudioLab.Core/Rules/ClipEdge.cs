using System.Text.Json.Serialization;
using AudioLab.Core.Model;

namespace AudioLab.Core.Rules;

/// <summary>Which edge of an audio clip a trim moves.</summary>
[JsonConverter(typeof(StrictEnumConverter<ClipEdge>))]
public enum ClipEdge
{
    /// <summary>The left edge: the clip starts later and the audio after it stays put.</summary>
    [JsonStringEnumMemberName("start")]
    Start,

    /// <summary>The right edge: the clip ends earlier or later.</summary>
    [JsonStringEnumMemberName("end")]
    End,
}
