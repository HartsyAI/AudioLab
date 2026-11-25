namespace AudioLab.Core.Transcription;

/// <summary>The bounds of a transcript and of a model's answer (D94, D98).</summary>
public static class TranscriptionLimits
{
    /// <summary>Most segments one transcript holds.</summary>
    public const int MaxSegments = 200_000;

    /// <summary>Most characters of text one segment holds.</summary>
    public const int MaxTextChars = 2_000;

    /// <summary>Most segments one model answer for a window holds.</summary>
    public const int MaxSegmentsPerWindow = 1_000;

    /// <summary>Most characters of a word.</summary>
    public const int MaxWordChars = 200;

    /// <summary>Most bytes of a stored transcript file.</summary>
    public const long MaxTranscriptBytes = 64L * 1024 * 1024;

    /// <summary>Bytes kept back from <see cref="MaxTranscriptBytes"/> for the document's own fields.</summary>
    public const long TranscriptOverheadBytes = 4096;

    /// <summary>How far outside its window a model's segment may fall, in seconds, before the answer is refused.</summary>
    public const double WindowToleranceSeconds = 0.5;

    /// <summary>Most characters of a model's id.</summary>
    public const int MaxModelIdChars = 128;
}
