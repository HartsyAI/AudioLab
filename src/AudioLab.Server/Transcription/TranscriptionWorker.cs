namespace AudioLab.Server.Transcription;

/// <summary>The background loop that runs transcription jobs, one chunk at a time. Requests never wait for it (D95).</summary>
/// <remarks>
/// The server's stop ends the loop (D93). When it begins, no retry starts and the chunk in flight gets one model timeout to finish; after
/// that it is abandoned, and the running jobs are paused from their last complete chunk. A failure of a chunk fails its job, and the loop
/// goes on.
/// </remarks>
public sealed class TranscriptionWorker(TranscriptionJobs jobs, TranscriptionOptions options, ILogger<TranscriptionWorker> logger)
    : BackgroundService
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using CancellationTokenSource abandon = new();
        using CancellationTokenRegistration stop = stoppingToken.Register(() =>
        {
            jobs.BeginStop();
            abandon.CancelAfter(options.ModelTimeout);
        });
        try
        {
            await WhenLoadedAsync();
            while (!stoppingToken.IsCancellationRequested)
            {
                if (await RunOneTurnAsync(abandon.Token))
                {
                    continue;
                }
                await jobs.WaitForWorkAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // The server is stopping: the chunk in flight has finished or was abandoned, and the running jobs are paused below.
        }
        finally
        {
            await jobs.PauseRunningAsync(CancellationToken.None);
        }
    }

    private async Task WhenLoadedAsync()
    {
        try
        {
            await jobs.WhenLoadedAsync();
        }
        catch (Exception exception)
        {
            TranscriptionLog.WorkerFailed(logger, exception.GetType().Name);
        }
    }

    private async Task<bool> RunOneTurnAsync(CancellationToken abandon)
    {
        try
        {
            return await jobs.StepAsync(abandon);
        }
        catch (Exception exception)
        {
            TranscriptionLog.WorkerFailed(logger, exception.GetType().Name);
            return false;
        }
    }
}
