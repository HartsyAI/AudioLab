using System.Text.Json.Serialization;
using AudioLab.Core.Model;

namespace AudioLab.Core.Api;

/// <summary>How the samples of an exported WAV file are stored.</summary>
[JsonConverter(typeof(StrictEnumConverter<ExportFormat>))]
public enum ExportFormat
{
    /// <summary>16-bit integer PCM.</summary>
    [JsonStringEnumMemberName("pcm16")]
    Pcm16,

    /// <summary>24-bit integer PCM.</summary>
    [JsonStringEnumMemberName("pcm24")]
    Pcm24,

    /// <summary>32-bit float, which keeps every sample as rendered, even past full scale.</summary>
    [JsonStringEnumMemberName("float32")]
    Float32,
}
