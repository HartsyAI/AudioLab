using System.Text.Json.Serialization;
using AudioLab.Core.Model;

namespace AudioLab.Core.Api;

/// <summary>The noise added before a sample is rounded to an integer in an exported file; a float file ignores it.</summary>
[JsonConverter(typeof(StrictEnumConverter<ExportDither>))]
public enum ExportDither
{
    /// <summary>No noise.</summary>
    [JsonStringEnumMemberName("none")]
    None,

    /// <summary>Noise spread evenly over one step.</summary>
    [JsonStringEnumMemberName("rectangular")]
    Rectangular,

    /// <summary>Noise with a triangular spread over two steps.</summary>
    [JsonStringEnumMemberName("triangular")]
    Triangular,
}
