namespace AudioLab.Server.Media;

/// <summary>The peaks of a file are being rebuilt and were not ready within the wait; the client should ask again.</summary>
public sealed class MediaPeaksPendingException(int retryAfterSeconds) : Exception("The peaks are being rebuilt; ask again in a moment.")
{
    /// <summary>How many seconds the client should wait before it asks again.</summary>
    public int RetryAfterSeconds { get; } = retryAfterSeconds;
}
