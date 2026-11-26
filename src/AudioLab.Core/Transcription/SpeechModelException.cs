namespace AudioLab.Core.Transcription;

/// <summary>A model that could not answer a window; a retryable one may succeed when tried again.</summary>
/// <remarks>
/// The message becomes the job's reason, so it never carries a path or an engine detail. The detail is in the inner exception, for the log.
/// </remarks>
public sealed class SpeechModelException(string message, bool retryable, Exception? inner = null) : Exception(message, inner)
{
    /// <summary>Whether a retry of the same window may succeed, such as after a timeout or a busy GPU.</summary>
    public bool Retryable { get; } = retryable;
}
