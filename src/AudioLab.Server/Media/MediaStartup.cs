namespace AudioLab.Server.Media;

/// <summary>Sweeps bytes that have no record when the host starts, so it happens at start and not at the first request.</summary>
public sealed class MediaStartup(MediaStore store) : IHostedService
{
    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        store.SweepOrphans();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
