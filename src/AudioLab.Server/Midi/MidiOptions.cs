namespace AudioLab.Server.Midi;

/// <summary>What the MIDI routes accept, under <c>AudioLab:Midi</c>.</summary>
public sealed record MidiOptions
{
    /// <summary>The largest .mid file an import reads, in bytes; the default is 16 MiB, and the codec's own limit is 64 MiB.</summary>
    /// <remarks>Whatever the file holds, the project it goes into is limited too: a project file is 32 MiB by default.</remarks>
    public long MaxFileBytes { get; init; } = 16L * 1024 * 1024;

    /// <summary>The longest an import may take to receive the whole file, from the first byte it reads; the default is two minutes.</summary>
    /// <remarks>The file is read before an import takes a place in the gate, so a slow upload costs memory and a connection, never a place.</remarks>
    public TimeSpan MaxReadTime { get; init; } = TimeSpan.FromMinutes(2);

    /// <summary>The most imports and exports one user may have running at once; one more is refused with 429.</summary>
    public int MaxConcurrentPerUser { get; init; } = 2;

    /// <summary>The most imports and exports the server runs at once, whoever asks; one more is refused with 429.</summary>
    public int MaxConcurrent { get; init; } = Math.Max(2, Environment.ProcessorCount);
}
