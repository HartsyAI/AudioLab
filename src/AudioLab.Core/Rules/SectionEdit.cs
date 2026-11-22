using System.Text.Json.Serialization;
using AudioLab.Core.Model;

namespace AudioLab.Core.Rules;

/// <summary>How a section is dragged: as a whole, or by one edge.</summary>
[JsonConverter(typeof(StrictEnumConverter<SectionEdit>))]
public enum SectionEdit
{
    /// <summary>The whole section moves and keeps its length.</summary>
    [JsonStringEnumMemberName("move")]
    Move,

    /// <summary>The start edge moves.</summary>
    [JsonStringEnumMemberName("start")]
    Start,

    /// <summary>The end edge moves.</summary>
    [JsonStringEnumMemberName("end")]
    End,
}
