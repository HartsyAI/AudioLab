namespace AudioLab.Server.Tests;

/// <summary>A clock that moves only when a test says so, and runs the timers that fall due as it moves.</summary>
public sealed class ManualTimeProvider : TimeProvider
{
    private readonly List<ManualTimer> _timers = [];
    private readonly object _sync = new();
    private DateTimeOffset _now = DateTimeOffset.UtcNow;

    public override DateTimeOffset GetUtcNow()
    {
        lock (_sync)
        {
            return _now;
        }
    }

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        ManualTimer timer = new(this, callback, state);
        timer.Change(dueTime, period);
        lock (_sync)
        {
            _timers.Add(timer);
        }
        return timer;
    }

    public void Advance(TimeSpan by)
    {
        DateTimeOffset target = GetUtcNow() + by;
        while (true)
        {
            ManualTimer? next;
            lock (_sync)
            {
                next = _timers.Where(timer => timer.Due is { } due && due <= target).OrderBy(timer => timer.Due).FirstOrDefault();
                if (next is null)
                {
                    _now = target;
                    return;
                }
                _now = next.Due!.Value;
                next.Due = null;
            }
            next.Fire();
        }
    }

    /// <summary>How many timers are armed.</summary>
    public int PendingTimers
    {
        get
        {
            lock (_sync)
            {
                return _timers.Count(timer => timer.Due is not null);
            }
        }
    }

    internal DateTimeOffset Now => GetUtcNow();
}
