using System.Text.Json.Serialization;
using AudioLab.Core.Model;

namespace AudioLab.Server.Mcp;

/// <summary>The part of a project get_project reads.</summary>
[JsonConverter(typeof(StrictEnumConverter<ProjectSection>))]
public enum ProjectSection
{
    /// <summary>Counts, and each track's id, name, kind and number of clips.</summary>
    [JsonStringEnumMemberName("summary")]
    Summary,

    /// <summary>Everything about the project but its markers, sections, groups, drum machine and tracks.</summary>
    [JsonStringEnumMemberName("settings")]
    Settings,

    /// <summary>The markers.</summary>
    [JsonStringEnumMemberName("markers")]
    Markers,

    /// <summary>The arrangement sections.</summary>
    [JsonStringEnumMemberName("sections")]
    Sections,

    /// <summary>The track groups.</summary>
    [JsonStringEnumMemberName("groups")]
    Groups,

    /// <summary>The drum machine.</summary>
    [JsonStringEnumMemberName("beat_pattern")]
    BeatPattern,

    /// <summary>The tracks, each without its clips; one track with <c>track</c>.</summary>
    [JsonStringEnumMemberName("tracks")]
    Tracks,

    /// <summary>The audio clips of a track.</summary>
    [JsonStringEnumMemberName("audio_clips")]
    AudioClips,

    /// <summary>The MIDI clips of a track, each without its notes.</summary>
    [JsonStringEnumMemberName("midi_clips")]
    MidiClips,

    /// <summary>The notes of a MIDI clip.</summary>
    [JsonStringEnumMemberName("notes")]
    Notes,

    /// <summary>The control changes of a MIDI clip.</summary>
    [JsonStringEnumMemberName("control_changes")]
    ControlChanges,
}
