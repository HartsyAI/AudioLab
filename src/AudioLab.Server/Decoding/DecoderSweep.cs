namespace AudioLab.Server.Decoding;

/// <summary>Sweeps the decoder's temporary folder when the server starts, so what a crash left never meets a new decode.</summary>
public sealed class DecoderSweep(DecoderLocation location) : IHostedService
{
    /// <summary>Removes the leftovers of an interrupted run.</summary>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        location.SweepLeftovers();
        return Task.CompletedTask;
    }

    /// <summary>Nothing to stop: the sweep is one step at start.</summary>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
