namespace AudioLab.Web.Tests;

/// <summary>A clock that moves only when a test says so, and fires the timers it passes.</summary>
internal sealed class ManualClock : TimeProvider
{
    private readonly List<ManualTimer> _timers = [];

    private TimeSpan _now = TimeSpan.Zero;

    public void Advance(TimeSpan by)
    {
        _now += by;
        foreach (ManualTimer timer in _timers.Where(timer => timer.Live && timer.Due <= _now).ToList())
        {
            timer.Live = false;
            timer.Callback(timer.State);
        }
    }

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        ManualTimer timer = new(callback, state, _now + dueTime);
        _timers.Add(timer);
        return timer;
    }

    private sealed class ManualTimer(TimerCallback callback, object? state, TimeSpan due) : ITimer
    {
        public TimerCallback Callback { get; } = callback;

        public object? State { get; } = state;

        public TimeSpan Due { get; } = due;

        public bool Live { get; set; } = true;

        public bool Change(TimeSpan dueTime, TimeSpan period) => false;

        public void Dispose() => Live = false;

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
