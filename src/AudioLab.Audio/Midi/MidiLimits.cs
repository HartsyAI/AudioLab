namespace AudioLab.Audio.Midi;

/// <summary>What the MIDI codec accepts and refuses, so that a hostile file cannot make it allocate or run without bound.</summary>
public static class MidiLimits
{
    /// <summary>The largest file the reader looks at, in bytes.</summary>
    public const int MaxFileBytes = 64 * 1024 * 1024;

    /// <summary>The most tracks a file may declare and hold.</summary>
    public const int MaxTracks = 1024;

    /// <summary>The most notes a file may hold, as many as a clip may.</summary>
    public const int MaxNotes = 1_000_000;

    /// <summary>The most controller events a file may hold, as many as a clip may.</summary>
    public const int MaxControlChanges = 1_000_000;

    /// <summary>The most tempo events a file may hold, as many as a project's tempo map may.</summary>
    public const int MaxTempoEvents = 100_000;

    /// <summary>The longest a time may be, in seconds, which is the project's limit.</summary>
    public const double MaxSeconds = 1_000_000;

    /// <summary>The ticks in a quarter note that the writer uses.</summary>
    public const int WrittenTicksPerQuarter = 480;

    /// <summary>The largest value a variable-length quantity of four bytes holds, which is the most a file may use.</summary>
    public const long MaxVariableLength = 0x0FFFFFFF;

    /// <summary>The shortest tempo event the reader accepts, in microseconds a quarter note, which is 1000 beats a minute.</summary>
    public const int MinMicrosecondsPerQuarter = 60_000;

    /// <summary>The longest tempo event a file can hold, in microseconds a quarter note, which is about 3.58 beats a minute.</summary>
    public const int MaxMicrosecondsPerQuarter = 0xFFFFFF;
}
