namespace AudioLab.Web.Playback;

/// <summary>What the worklet counted on the audio thread, as it reported.</summary>
public sealed record WorkletStats
{
    /// <summary>The context's frame counter.</summary>
    public double Frame { get; init; }

    /// <summary>The context's clock, in seconds.</summary>
    public double Time { get; init; }

    /// <summary>Render quanta the audio thread has run.</summary>
    public long Quanta { get; init; }

    /// <summary>Frames played since the ring first filled; the length of a run is measured in these.</summary>
    public long Counted { get; init; }

    /// <summary>Runs of consecutive quanta that had too little audio.</summary>
    public long Events { get; init; }

    /// <summary>Frames played as silence for lack of audio.</summary>
    public long Missed { get; init; }

    /// <summary>Frames in the ring now.</summary>
    public int Fill { get; init; }

    /// <summary>The fewest frames the ring held at the start of a quantum.</summary>
    public int MinFill { get; init; }

    /// <summary>The most frames the ring held at the start of a quantum.</summary>
    public int MaxFill { get; init; }

    /// <summary>Frames that arrived with the ring full; any is a protocol error.</summary>
    public long Overflow { get; init; }

    /// <summary>Whether the ring has filled once.</summary>
    public bool Primed { get; init; }
}
