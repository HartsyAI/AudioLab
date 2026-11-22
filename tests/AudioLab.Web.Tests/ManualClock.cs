namespace AudioLab.Web.Tests;

/// <summary>A clock that moves only when a test says so, and fires the timers it passes.</summary>
internal sealed class ManualClock : TimeProvider
{
    private readonly List<ManualTimer> _timers = [];

    private TimeSpan _now = TimeSpan.Zero;

    /// <summary>Advance.</summary>
    public void Advance(TimeSpan by)
    {
        _now += by;
        foreach (ManualTimer timer in _timers.Where(timer => timer.Live && timer.Due <= _now).ToList())
        {
            timer.Live = false;
            timer.Callback(timer.State);
        }
    }

    /// <summary>Create timer.</summary>
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        ManualTimer timer = new(callback, state, _now + dueTime);
        _timers.Add(timer);
        return timer;
    }

    private sealed class ManualTimer(TimerCallback callback, object? state, TimeSpan due) : ITimer
    {
        /// <summary>Callback.</summary>
        public TimerCallback Callback { get; } = callback;

        /// <summary>State.</summary>
        public object? State { get; } = state;

        /// <summary>Due.</summary>
        public TimeSpan Due { get; } = due;

        /// <summary>Live.</summary>
        public bool Live { get; set; } = true;

        /// <summary>Change.</summary>
        public bool Change(TimeSpan dueTime, TimeSpan period) => false;

        /// <summary>Dispose.</summary>
        public void Dispose() => Live = false;

        /// <summary>Dispose async.</summary>
        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
