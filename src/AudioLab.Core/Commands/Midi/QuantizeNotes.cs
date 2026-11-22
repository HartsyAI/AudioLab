using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>Snaps notes to the project grid at the tempo where the clip starts: starts to a step, lengths to whole steps.</summary>
public sealed record QuantizeNotes : Command
{
    /// <summary>The MIDI clip the notes belong to.</summary>
    [Description("The MIDI clip the notes belong to.")]
    public required ClipId Clip { get; init; }

    /// <summary>The notes to snap, from 1 to 1000000; every note of the clip when absent.</summary>
    [Description("The notes to snap, from 1 to 1000000; every note of the clip when absent.")]
    [JsonConverter(typeof(ValueListJsonConverter<NoteId>))]
    [MinLength(1)]
    [MaxLength(ProjectLimits.MaxListItems)]
    public ValueList<NoteId>? Notes { get; init; }
}
