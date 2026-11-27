namespace AudioLab.Server.Transcription;

/// <summary>A stored transcript, or a transcript about to be stored, is not one the server accepts (D94).</summary>
public sealed class TranscriptRefusedException(IReadOnlyList<string> problems)
    : Exception(problems.Count > 0 ? problems[0] : "The transcript is not accepted.")
{
    /// <summary>What is wrong, in words a user can read; at most 100 entries.</summary>
    public IReadOnlyList<string> Problems { get; } = problems;
}
