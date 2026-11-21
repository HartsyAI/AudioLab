namespace AudioLab.Core.Model;

/// <summary>A track: an audio track, an instrument track or an aux bus.</summary>
public sealed record Track
{
    /// <summary>The track's id.</summary>
    public required TrackId Id { get; init; }

    /// <summary>The track's name.</summary>
    public required string Name { get; init; }

    /// <summary>What the track holds.</summary>
    public required TrackKind Kind { get; init; }

    /// <summary>The track colour.</summary>
    public required string Color { get; init; }

    /// <summary>The fader gain, from 0 up; 1 is unity.</summary>
    public required double Volume { get; init; }

    /// <summary>The pan, from -1 (left) to 1 (right).</summary>
    public required double Pan { get; init; }

    /// <summary>Whether the track is silent.</summary>
    public required bool Muted { get; init; }

    /// <summary>Whether the track is soloed.</summary>
    public required bool Soloed { get; init; }

    /// <summary>Whether the track records on the transport's record.</summary>
    public required bool Armed { get; init; }

    /// <summary>Whether the live input is heard through the track.</summary>
    public required bool Monitor { get; init; }

    /// <summary>The group the track belongs to, or null.</summary>
    public GroupId? Group { get; init; }

    /// <summary>An emoji or a small image data URL shown on the track, or null.</summary>
    public string? Icon { get; init; }

    /// <summary>Where the track records from.</summary>
    public required TrackInput Input { get; init; }

    /// <summary>How many takes the track has recorded, used to name the next one.</summary>
    public required int TakeCount { get; init; }

    /// <summary>The sound source of an instrument track, or null.</summary>
    public Instrument? Instrument { get; init; }

    /// <summary>The audio clips; later clips layer over earlier ones.</summary>
    public required IReadOnlyList<AudioClip> Clips { get; init; }

    /// <summary>The MIDI clips; later clips layer over earlier ones.</summary>
    public required IReadOnlyList<MidiClip> MidiClips { get; init; }

    /// <summary>The insert chain, in signal order.</summary>
    public required IReadOnlyList<Effect> Effects { get; init; }

    /// <summary>The volume and pan envelopes.</summary>
    public required TrackAutomation Automation { get; init; }

    /// <summary>The sends to aux buses.</summary>
    public required IReadOnlyList<Send> Sends { get; init; }

    /// <summary>How the track draws.</summary>
    public required TrackView View { get; init; }
}
