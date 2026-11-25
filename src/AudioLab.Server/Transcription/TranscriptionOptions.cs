namespace AudioLab.Server.Transcription;

/// <summary>How transcription jobs share the server, under <c>AudioLab:Transcription</c> (D95).</summary>
public sealed record TranscriptionOptions
{
    /// <summary>The most running jobs one user may have.</summary>
    public int MaxRunningPerUser { get; init; } = 1;

    /// <summary>The most running jobs the server has; the worker takes one chunk at a time, in turn among them.</summary>
    public int MaxRunning { get; init; } = 2;

    /// <summary>The most queued or running jobs one user may have before a new start is refused.</summary>
    public int MaxQueuedPerUser { get; init; } = 20;

    /// <summary>How many times a chunk is tried again after a retryable model error.</summary>
    public int Retries { get; init; } = 2;

    /// <summary>How long the worker waits before a retry; read from the server's clock.</summary>
    public TimeSpan RetryDelay { get; init; } = TimeSpan.FromSeconds(5);
}
