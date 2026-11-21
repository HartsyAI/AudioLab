namespace AudioLab.Web.Session;

/// <summary>One track: its header state and the clips on its lane.</summary>
public sealed class TrackModel
{
    /// <summary>One-based track number.</summary>
    public required int Number { get; init; }

    /// <summary>Track name.</summary>
    public required string Name { get; init; }

    /// <summary>Source line under the name.</summary>
    public required string Source { get; init; }

    /// <summary>Name of the colour token that identifies the track, for example --track-2.</summary>
    public required string ColourToken { get; init; }

    /// <summary>What the track holds.</summary>
    public required TrackKind Kind { get; init; }

    /// <summary>Volume readout in dB.</summary>
    public required string DbText { get; init; }

    /// <summary>Volume slider position, percent.</summary>
    public required int Volume { get; init; }

    /// <summary>Left meter level, percent.</summary>
    public required int MeterLeft { get; init; }

    /// <summary>Right meter level, percent.</summary>
    public required int MeterRight { get; init; }

    /// <summary>Whether the track is muted.</summary>
    public bool Muted { get; init; }

    /// <summary>Whether the track is armed to record.</summary>
    public bool Armed { get; init; }

    /// <summary>Whether input monitoring is on.</summary>
    public bool Monitoring { get; init; }

    /// <summary>Whether the track carries the selection.</summary>
    public bool Selected { get; init; }

    /// <summary>The clips on this track's lane.</summary>
    public List<ClipModel> Clips { get; } = [];
}
