namespace AudioLab.Core.Transcription;

/// <summary>A model that could not answer a window; a retryable one may succeed when tried again.</summary>
public sealed class SpeechModelException(string message, bool retryable) : Exception(message)
{
    /// <summary>Whether a retry of the same window may succeed, such as after a timeout or a busy GPU.</summary>
    public bool Retryable { get; } = retryable;
}
