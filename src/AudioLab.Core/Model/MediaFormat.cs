using System.Text.Json.Serialization;

namespace AudioLab.Core.Model;

/// <summary>The container a media file is stored in.</summary>
[JsonConverter(typeof(StrictEnumConverter<MediaFormat>))]
public enum MediaFormat
{
    /// <summary>RIFF WAVE. Compressed formats come with the audio engine.</summary>
    [JsonStringEnumMemberName("wav")]
    Wav,
}
