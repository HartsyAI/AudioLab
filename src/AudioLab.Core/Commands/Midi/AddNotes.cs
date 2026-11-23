using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Core.Commands;

/// <summary>Adds notes to a MIDI clip in one step, all or none; the server allocates their ids in list order, which the result lists.</summary>
public sealed record AddNotes : Command
{
    /// <summary>The MIDI clip to add the notes to.</summary>
    [Description("The MIDI clip to add the notes to.")]
    public required ClipId Clip { get; init; }

    /// <summary>The notes to add, 1 to 10,000, which get their ids in this order; each is checked as midi_note_add checks one.</summary>
    [Description(
        "The notes to add, 1 to 10000, which get their ids in this order. Each is checked as midi_note_add checks one; if any is refused, "
        + "none is added, and the refusal names it by its place in the list, from 0.")]
    [JsonConverter(typeof(ValueListJsonConverter<NewNote>))]
    [MinLength(1)]
    [MaxLength(NoteRules.MaxAddedNotes)]
    public required ValueList<NewNote> Notes { get; init; }
}
