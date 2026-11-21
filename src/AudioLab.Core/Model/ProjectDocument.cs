namespace AudioLab.Core.Model;

/// <summary>The project document, version 4. The server owns it; every edit is a command applied to it.</summary>
public sealed record ProjectDocument
{
    /// <summary>The version of this document format.</summary>
    public const int CurrentVersion = 4;

    /// <summary>The format version; always <see cref="CurrentVersion"/> here.</summary>
    public required int Version { get; init; }

    /// <summary>The project's id.</summary>
    public required ProjectId Id { get; init; }

    /// <summary>The user who owns the project. Every read and write of a project is scoped to it.</summary>
    public required UserId Owner { get; init; }

    /// <summary>The project's name.</summary>
    public required string Name { get; init; }

    /// <summary>The tempo from the start until the first tempo change, in beats per minute.</summary>
    public required double Bpm { get; init; }

    /// <summary>The tempo changes after the start, ordered by time.</summary>
    public required IReadOnlyList<TempoChange> TempoMap { get; init; }

    /// <summary>The time signature.</summary>
    public required TimeSignature TimeSignature { get; init; }

    /// <summary>The master gain, from 0 up; 1 is unity.</summary>
    public required double MasterVolume { get; init; }

    /// <summary>Whether the master limiter is on.</summary>
    public required bool MasterLimiter { get; init; }

    /// <summary>Whether the metronome clicks while the transport runs.</summary>
    public required bool Metronome { get; init; }

    /// <summary>The markers, ordered by time.</summary>
    public required IReadOnlyList<Marker> Markers { get; init; }

    /// <summary>The arrangement sections, ordered by start and never overlapping.</summary>
    public required IReadOnlyList<Section> Sections { get; init; }

    /// <summary>The track groups.</summary>
    public required IReadOnlyList<TrackGroup> Groups { get; init; }

    /// <summary>The drum machine.</summary>
    public required BeatPattern BeatPattern { get; init; }

    /// <summary>The tracks, top to bottom.</summary>
    public required IReadOnlyList<Track> Tracks { get; init; }

    /// <summary>How the arrangement opens.</summary>
    public required ProjectView View { get; init; }
}
