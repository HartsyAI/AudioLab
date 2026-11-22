using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>Puts notes at new starts and pitches, kept inside the clip; the targets are absolute, so a drag can come back.</summary>
public sealed record MoveNotes : Command
{
    /// <summary>The MIDI clip the notes belong to.</summary>
    [Description("The MIDI clip the notes belong to.")]
    public required ClipId Clip { get; init; }

    /// <summary>Each note to move with its new start (seconds from the start of the clip) and pitch.</summary>
    [Description("Each note to move with its new start (seconds from the start of the clip) and pitch.")]
    [JsonConverter(typeof(ValueListJsonConverter<NoteTarget>))]
    [MinLength(1)]
    [MaxLength(ProjectLimits.MaxListItems)]
    public required ValueList<NoteTarget> Notes { get; init; }
}
