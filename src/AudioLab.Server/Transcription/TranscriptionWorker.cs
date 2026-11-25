
namespace AudioLab.Server.Transcription;

/// <summary>The background loop that runs transcription jobs, one chunk at a time. Requests never wait for it (D95).</summary>
public sealed class TranscriptionWorker(TranscriptionJobs jobs, ILogger<TranscriptionWorker> logger) : BackgroundService
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await jobs.WhenLoadedAsync();
            while (!stoppingToken.IsCancellationRequested)
            {
                if (await RunOneTurnAsync(stoppingToken))
                {
                    continue;
                }
                await jobs.WaitForWorkAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // The server is stopping: the chunk in flight is abandoned and the jobs are paused below.
        }
        finally
        {
            await jobs.PauseRunningAsync(CancellationToken.None);
        }
    }

    private async Task<bool> RunOneTurnAsync(CancellationToken stoppingToken)
    {
        try
        {
            return await jobs.StepAsync(stoppingToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            TranscriptionLog.WorkerFailed(logger, exception.GetType().Name);
            return false;
        }
    }
}
