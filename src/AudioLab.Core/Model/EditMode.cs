using System.Text.Json.Serialization;

namespace AudioLab.Core.Model;

/// <summary>How edits move other material.</summary>
[JsonConverter(typeof(StrictEnumConverter<EditMode>))]
public enum EditMode
{
    /// <summary>Free placement, no snapping.</summary>
    [JsonStringEnumMemberName("slip")]
    Slip,

    /// <summary>Placement snaps to the grid.</summary>
    [JsonStringEnumMemberName("grid")]
    Grid,

    /// <summary>Material closes or opens gaps as clips move.</summary>
    [JsonStringEnumMemberName("shuffle")]
    Shuffle,
}
