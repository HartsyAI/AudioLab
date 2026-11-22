namespace AudioLab.Server.Tests;

/// <summary>A one-shot timer of <see cref="ManualTimeProvider"/>; it fires when the clock reaches its due time.</summary>
public sealed class ManualTimer(ManualTimeProvider provider, TimerCallback callback, object? state) : ITimer
{
    public DateTimeOffset? Due { get; set; }

    public bool Change(TimeSpan dueTime, TimeSpan period)
    {
        Due = dueTime == Timeout.InfiniteTimeSpan ? null : provider.Now + dueTime;
        return true;
    }

    public void Fire() => callback(state);

    public void Dispose() => Due = null;

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}
