namespace AudioLab.Core.Transcription;

/// <summary>The speech models the registry knows (D98). A model is used only when it is also installed on the server.</summary>
public static class SpeechModelCatalog
{
    /// <summary>The capability of speech-to-text.</summary>
    public const string SpeechCapability = "transcribe.speech";

    /// <summary>The model a user gets until they choose another (D98).</summary>
    public const string DefaultSpeechModel = "whisper:medium.en";

    /// <summary>Checks the entries once, when the server starts: an id must be 1 to <see cref="TranscriptionLimits.MaxModelIdChars"/>
    /// characters and not only spaces, and the folder and the language must be given.</summary>
    /// <exception cref="ArgumentException">An entry is not one the registry accepts.</exception>
    public static void Validate(IEnumerable<SpeechModelEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        foreach (SpeechModelEntry entry in entries)
        {
            if (string.IsNullOrWhiteSpace(entry.Id) || entry.Id.Length > TranscriptionLimits.MaxModelIdChars)
            {
                throw new ArgumentException(
                    $"A speech model id must be 1 to {TranscriptionLimits.MaxModelIdChars} characters and not only spaces.",
                    nameof(entries));
            }
            if (string.IsNullOrWhiteSpace(entry.Folder) || string.IsNullOrWhiteSpace(entry.Language))
            {
                throw new ArgumentException("A speech model needs a folder and a language.", nameof(entries));
            }
        }
    }

    /// <summary>Every entry the server knows, in the order the choices are offered.</summary>
    public static IReadOnlyList<SpeechModelEntry> Entries { get; } =
    [
        new(DefaultSpeechModel, SpeechCapability, "openai--whisper-medium.en", "en"),
    ];
}
