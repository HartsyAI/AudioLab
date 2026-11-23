namespace AudioLab.Server.Media;

/// <summary>At start: sweeps bytes that have no record, then rebuilds in the background the peaks that records lack.</summary>
public sealed class MediaStartup(MediaStore store, PeakRebuilder rebuilder, ILogger<MediaStartup> logger) : IHostedService
{
    private CancellationTokenSource? _stop;
    private Task _rebuilding = Task.CompletedTask;

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        store.SweepOrphans();
        _stop = new CancellationTokenSource();
        CancellationToken stop = _stop.Token;
        _rebuilding = Task.Run(() => RebuildAsync(stop), stop);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_stop is not null)
        {
            await _stop.CancelAsync();
        }
        try
        {
            await _rebuilding.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Stopping: the rest is rebuilt at the next start.
        }
    }

    private async Task RebuildAsync(CancellationToken stop)
    {
        foreach ((string data, string peaks) in store.FindMissingPeaks())
        {
            if (stop.IsCancellationRequested)
            {
                return;
            }
            try
            {
                await rebuilder.EnsureAsync(data, peaks);
            }
            catch (Exception exception) when (exception is MediaRejectedException or IOException or UnauthorizedAccessException)
            {
                // A damaged file is refused when someone asks for it; here it only keeps the others from being rebuilt.
                MediaLog.PeaksNotRebuilt(logger, Path.GetFileName(peaks), exception.GetType().Name);
            }
        }
    }
}
