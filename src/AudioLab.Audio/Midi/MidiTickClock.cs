namespace AudioLab.Audio.Midi;

/// <summary>Turns ticks into seconds through the tempo events of a file; before its first tempo the tempo is 120 beats a minute.</summary>
internal sealed class MidiTickClock
{
    /// <summary>The tempo a file has until its first tempo event, in microseconds a quarter note.</summary>
    public const int DefaultMicroseconds = 500_000;

    private readonly long[] _ticks;
    private readonly int[] _microseconds;
    private readonly double[] _seconds;
    private readonly int _ticksPerQuarter;

    /// <summary>Builds the clock from tempo events in any order, of which the last at a tick holds.</summary>
    public MidiTickClock(IReadOnlyList<(long Tick, int Microseconds)> events, int ticksPerQuarter)
    {
        _ticksPerQuarter = ticksPerQuarter;
        (long Tick, int Microseconds)[] ordered = [.. events.Select((tempo, index) => (tempo, index))
            .OrderBy(entry => entry.tempo.Tick).ThenBy(entry => entry.index).Select(entry => entry.tempo)];
        List<(long Tick, int Microseconds)> kept = [];
        foreach ((long tick, int microseconds) in ordered)
        {
            if (kept.Count > 0 && kept[^1].Tick == tick)
            {
                kept[^1] = (tick, microseconds);
            }
            else
            {
                kept.Add((tick, microseconds));
            }
        }
        if (kept.Count == 0 || kept[0].Tick > 0)
        {
            kept.Insert(0, (0, DefaultMicroseconds));
        }
        _ticks = new long[kept.Count];
        _microseconds = new int[kept.Count];
        _seconds = new double[kept.Count];
        for (int i = 0; i < kept.Count; i++)
        {
            _ticks[i] = kept[i].Tick;
            _microseconds[i] = kept[i].Microseconds;
            _seconds[i] = i == 0 ? 0 : _seconds[i - 1] + SecondsIn(_ticks[i] - _ticks[i - 1], _microseconds[i - 1]);
        }
    }

    /// <summary>How many tempo segments there are, the first of which starts at tick 0.</summary>
    public int Count => _ticks.Length;

    /// <summary>The first tick of a segment.</summary>
    public long TickOf(int segment) => _ticks[segment];

    /// <summary>The tempo of a segment, in beats per minute.</summary>
    public double BpmOf(int segment) => 60_000_000.0 / _microseconds[segment];

    /// <summary>The time of a tick, in seconds from the start of the file.</summary>
    public double Seconds(long tick)
    {
        int low = 0;
        int high = _ticks.Length - 1;
        while (low < high)
        {
            int middle = (low + high + 1) >>> 1;
            if (_ticks[middle] <= tick)
            {
                low = middle;
            }
            else
            {
                high = middle - 1;
            }
        }
        return _seconds[low] + SecondsIn(tick - _ticks[low], _microseconds[low]);
    }

    private double SecondsIn(long ticks, int microseconds) => ticks * (double)microseconds / (1_000_000.0 * _ticksPerQuarter);
}
