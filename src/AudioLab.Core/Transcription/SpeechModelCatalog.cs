namespace AudioLab.Core.Transcription;

/// <summary>The speech models the registry knows (D98). A model is used only when it is also installed on the server.</summary>
public static class SpeechModelCatalog
{
    /// <summary>The capability of speech-to-text.</summary>
    public const string SpeechCapability = "transcribe.speech";

    /// <summary>The model a user gets until they choose another (D98).</summary>
    public const string DefaultSpeechModel = "whisper:medium.en";

    /// <summary>Every entry the server knows, in the order the choices are offered.</summary>
    public static IReadOnlyList<SpeechModelEntry> Entries { get; } =
    [
        new(DefaultSpeechModel, SpeechCapability, "openai--whisper-medium.en", "en"),
    ];
}
