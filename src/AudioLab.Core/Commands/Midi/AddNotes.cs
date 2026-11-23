using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>Adds notes to a MIDI clip in one step, all or none; the server allocates their ids in list order, which the result lists.</summary>
public sealed record AddNotes : Command
{
    /// <summary>The MIDI clip to add the notes to.</summary>
    [Description("The MIDI clip to add the notes to.")]
    public required ClipId Clip { get; init; }

    /// <summary>The notes to add; each is checked as midi_note_add checks one, and none is added if any is refused.</summary>
    [Description("The notes to add; each is checked as midi_note_add checks one, and none is added if any is refused.")]
    [JsonConverter(typeof(ValueListJsonConverter<NewNote>))]
    [MinLength(1)]
    [MaxLength(ProjectLimits.MaxListItems)]
    public required ValueList<NewNote> Notes { get; init; }
}
