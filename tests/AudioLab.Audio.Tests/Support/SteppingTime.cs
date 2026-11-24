namespace AudioLab.Audio.Tests.Support;

/// <summary>A clock that moves a set time at each reading, or by hand, so a test decides how long a step "took".</summary>
public sealed class SteppingTime(TimeSpan step) : TimeProvider
{
    private long _ticks;

    /// <inheritdoc />
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    /// <summary>Moves the clock on.</summary>
    public void Advance(TimeSpan by) => _ticks += by.Ticks;

    /// <inheritdoc />
    public override long GetTimestamp()
    {
        _ticks += step.Ticks;
        return _ticks;
    }
}
