namespace AudioLab.Server.Render;

/// <summary>What an export holds until it is done: its place in the gate and the tokens that stop it.</summary>
/// <param name="Lease">The export's place in the gate.</param>
/// <param name="Limit">Cancels when the export has run as long as it may.</param>
/// <param name="Linked">Cancels when the client goes away or the time limit passes.</param>
/// <param name="RequestAborted">Cancels when the client goes away.</param>
internal sealed record Hold(
    IDisposable Lease,
    CancellationTokenSource Limit,
    CancellationTokenSource Linked,
    CancellationToken RequestAborted) : IDisposable
{
    /// <summary>Gives up the place in the gate and the tokens.</summary>
    public void Dispose()
    {
        Lease.Dispose();
        Linked.Dispose();
        Limit.Dispose();
    }
}
