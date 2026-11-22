namespace AudioLab.Server.Tests;

/// <summary>Holds a command's handler, which runs under its session's lock, until a test lets it go.</summary>
public sealed class SlowGate
{
    private readonly ManualResetEventSlim _open = new(false);
    private readonly TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task Entered => _entered.Task;

    public void Pause()
    {
        _entered.TrySetResult();
        _open.Wait(TimeSpan.FromSeconds(30));
    }

    public void Release() => _open.Set();
}
