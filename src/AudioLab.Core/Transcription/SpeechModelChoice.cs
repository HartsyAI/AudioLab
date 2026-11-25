using System.Text.Json.Serialization;

namespace AudioLab.Core.Transcription;

/// <summary>The speech model a user chose, as stored in the user's transcription folder (D98).</summary>
public sealed record SpeechModelChoice([property: JsonPropertyName("model")] string Model);
