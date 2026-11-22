using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>Gives notes new lengths, each at least 0.02 seconds; the lengths are absolute, so a drag can come back.</summary>
public sealed record ResizeNotes : Command
{
    /// <summary>The MIDI clip the notes belong to.</summary>
    [Description("The MIDI clip the notes belong to.")]
    public required ClipId Clip { get; init; }

    /// <summary>Each note to resize with its new length in seconds.</summary>
    [Description("Each note to resize with its new length in seconds.")]
    [JsonConverter(typeof(ValueListJsonConverter<NoteLength>))]
    [MinLength(1)]
    [MaxLength(ProjectLimits.MaxListItems)]
    public required ValueList<NoteLength> Notes { get; init; }
}
