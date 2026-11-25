namespace AudioLab.Web.Tests;

/// <summary>A clock that moves only when a test says so, and fires the timers it passes.</summary>
/// <remarks>The timers are created and fired from the store's own threads, so the list and the due timers are guarded by one lock.</remarks>
internal sealed class ManualClock : TimeProvider
{
    private readonly object _gate = new();

    private readonly List<ManualTimer> _timers = [];

    private TimeSpan _now = TimeSpan.Zero;

    /// <summary>Advance.</summary>
    public void Advance(TimeSpan by)
    {
        List<ManualTimer> due;
        lock (_gate)
        {
            _now += by;
            due = _timers.Where(timer => timer.Live && timer.Due <= _now).ToList();
            foreach (ManualTimer timer in due)
            {
                timer.Live = false;
            }
        }
        foreach (ManualTimer timer in due)
        {
            timer.Callback(timer.State);
        }
    }

    /// <summary>Create timer.</summary>
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        lock (_gate)
        {
            ManualTimer timer = new(callback, state, _now + dueTime, _gate);
            _timers.Add(timer);
            return timer;
        }
    }

    private sealed class ManualTimer(TimerCallback callback, object? state, TimeSpan due, object gate) : ITimer
    {
        /// <summary>Callback.</summary>
        public TimerCallback Callback { get; } = callback;

        /// <summary>State.</summary>
        public object? State { get; } = state;

        /// <summary>Due.</summary>
        public TimeSpan Due { get; } = due;

        /// <summary>Live, read and written under the clock's lock.</summary>
        public bool Live { get; set; } = true;

        /// <summary>Change.</summary>
        public bool Change(TimeSpan dueTime, TimeSpan period) => false;

        /// <summary>Dispose.</summary>
        public void Dispose()
        {
            lock (gate)
            {
                Live = false;
            }
        }

        /// <summary>Dispose async.</summary>
        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
