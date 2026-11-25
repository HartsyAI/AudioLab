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

    /// <summary>How long one model call may take before it is abandoned and counted as a retryable error (D93).</summary>
    public TimeSpan ModelTimeout { get; init; } = TimeSpan.FromMinutes(10);

    /// <summary>Checks the limits once, when the server starts, so a bad value stops the start and does not stop jobs silently (D95).</summary>
    /// <exception cref="ArgumentOutOfRangeException">A limit is outside the range the design allows.</exception>
    public void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(MaxRunningPerUser, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(MaxRunning, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(MaxQueuedPerUser, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(Retries, 0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(RetryDelay, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(ModelTimeout, TimeSpan.Zero);
    }
}
