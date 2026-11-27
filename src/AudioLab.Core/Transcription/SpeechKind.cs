using System.Text.Json.Serialization;

namespace AudioLab.Core.Transcription;

/// <summary>What a model heard a segment to be: spoken words, sung words, or something else such as a sound or a tag.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<SpeechKind>))]
public enum SpeechKind
{
    /// <summary>Spoken words.</summary>
    [JsonStringEnumMemberName("speech")]
    Speech,

    /// <summary>Sung words, shown as lyrics.</summary>
    [JsonStringEnumMemberName("singing")]
    Singing,

    /// <summary>Anything else the model reports as text.</summary>
    [JsonStringEnumMemberName("other")]
    Other,
}
