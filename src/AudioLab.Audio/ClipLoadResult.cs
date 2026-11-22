namespace AudioLab.Audio;

/// <summary>What fetching and decoding the clips cost.</summary>
public sealed record ClipLoadResult
{
    /// <summary>Clips loaded.</summary>
    public required int Clips { get; init; }

    /// <summary>Bytes received over HTTP, bodies only.</summary>
    public required long Bytes { get; init; }

    /// <summary>Milliseconds from the first request to the last clip decoded.</summary>
    public required double Milliseconds { get; init; }

    /// <summary>Bytes the decoded clips hold in memory.</summary>
    public required long DecodedBytes { get; init; }

    /// <summary>Seconds of audio in the clips, summed.</summary>
    public required double Seconds { get; init; }
}
