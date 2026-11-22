using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>Removes notes from a MIDI clip.</summary>
public sealed record DeleteNotes : Command
{
    /// <summary>The MIDI clip the notes belong to.</summary>
    [Description("The MIDI clip the notes belong to.")]
    public required ClipId Clip { get; init; }

    /// <summary>The notes to remove, from 1 to 1000000.</summary>
    [Description("The notes to remove, from 1 to 1000000.")]
    [JsonConverter(typeof(ValueListJsonConverter<NoteId>))]
    [MinLength(1)]
    [MaxLength(ProjectLimits.MaxListItems)]
    public required ValueList<NoteId> Notes { get; init; }
}
