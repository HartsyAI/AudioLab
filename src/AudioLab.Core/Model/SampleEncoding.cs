using System.Text.Json.Serialization;

namespace AudioLab.Core.Model;

/// <summary>How the samples of a media file are stored.</summary>
[JsonConverter(typeof(StrictEnumConverter<SampleEncoding>))]
public enum SampleEncoding
{
    /// <summary>Signed 16-bit integer PCM.</summary>
    [JsonStringEnumMemberName("pcm16")]
    Pcm16,

    /// <summary>Signed 24-bit integer PCM.</summary>
    [JsonStringEnumMemberName("pcm24")]
    Pcm24,

    /// <summary>Signed 32-bit integer PCM.</summary>
    [JsonStringEnumMemberName("pcm32")]
    Pcm32,

    /// <summary>IEEE 32-bit floating point.</summary>
    [JsonStringEnumMemberName("float32")]
    Float32,
}
