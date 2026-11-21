using System.Text.Json.Serialization;

namespace AudioLab.Core.Model;

/// <summary>A MIDI clip: notes and controller events placed on the timeline.</summary>
public sealed record MidiClip
{
    /// <summary>The clip's id.</summary>
    public required ClipId Id { get; init; }

    /// <summary>The clip's name.</summary>
    public required string Name { get; init; }

    /// <summary>Where the clip starts on the timeline, in seconds.</summary>
    public required double Start { get; init; }

    /// <summary>The clip length, in seconds.</summary>
    public required double Length { get; init; }

    /// <summary>The id the next note added to this clip takes. Ids of deleted notes are never reused.</summary>
    public required NoteId NextNoteId { get; init; }

    /// <summary>The notes, with times in seconds from the clip start.</summary>
    [JsonConverter(typeof(ValueListJsonConverter<MidiNote>))]
    public required ValueList<MidiNote> Notes { get; init; }

    /// <summary>The controller events such as the sustain pedal, ordered by time.</summary>
    [JsonConverter(typeof(ValueListJsonConverter<MidiControlChange>))]
    public required ValueList<MidiControlChange> ControlChanges { get; init; }

    /// <summary>Whether the clip is silent.</summary>
    public required bool Muted { get; init; }

    /// <summary>A colour that overrides the track colour, or null.</summary>
    public string? Color { get; init; }
}
