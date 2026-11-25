namespace AudioLab.Server.Transcription;

/// <summary>The background loop that runs transcription jobs, one chunk at a time. Requests never wait for it (D95).</summary>
/// <remarks>
/// Only the server's stop ends the loop (D93). A chunk in flight is finished before the stop pauses the running jobs; a failure of a chunk
/// fails its job, and the loop goes on.
/// </remarks>
public sealed class TranscriptionWorker(TranscriptionJobs jobs, ILogger<TranscriptionWorker> logger) : BackgroundService
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await WhenLoadedAsync();
            while (!stoppingToken.IsCancellationRequested)
            {
                if (await RunOneTurnAsync())
                {
                    continue;
                }
                await jobs.WaitForWorkAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // The server is stopping: the chunk in flight has finished, and the running jobs are paused below.
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

    private async Task<bool> RunOneTurnAsync()
    {
        try
        {
            return await jobs.StepAsync(CancellationToken.None);
        }
        catch (Exception exception)
        {
            TranscriptionLog.WorkerFailed(logger, exception.GetType().Name);
            return false;
        }
    }
}
