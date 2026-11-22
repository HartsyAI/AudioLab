using System.ComponentModel;
using System.Text.Json.Serialization;
using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>Snaps notes to the project grid at the tempo at the start of the clip: each start to the nearest step and each length to the nearest whole step, at least one.</summary>
public sealed record QuantizeNotes : Command
{
    /// <summary>The MIDI clip the notes belong to.</summary>
    [Description("The MIDI clip the notes belong to.")]
    public required ClipId Clip { get; init; }

    /// <summary>The notes to snap; every note of the clip when absent.</summary>
    [Description("The notes to snap; every note of the clip when absent.")]
    [JsonConverter(typeof(ValueListJsonConverter<NoteId>))]
    public ValueList<NoteId>? Notes { get; init; }
}
