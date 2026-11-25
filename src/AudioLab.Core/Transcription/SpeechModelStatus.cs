using System.Text.Json.Serialization;

namespace AudioLab.Core.Transcription;

/// <summary>The speech model a user uses, and the speech models installed on the server, for the speech-model route (D98).</summary>
public sealed record SpeechModelStatus(
    [property: JsonPropertyName("model")] string Model,
    [property: JsonPropertyName("installed")] IReadOnlyList<string> Installed);
