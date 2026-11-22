using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Core.Commands;

/// <summary>Moves notes up or down by semitones, to the ends of the pitch range.</summary>
public sealed record TransposeNotes : Command
{
    /// <summary>The MIDI clip the notes belong to.</summary>
    [Description("The MIDI clip the notes belong to.")]
    public required ClipId Clip { get; init; }

    /// <summary>The notes to transpose; every note of the clip when absent.</summary>
    [Description("The notes to transpose; every note of the clip when absent.")]
    [JsonConverter(typeof(ValueListJsonConverter<NoteId>))]
    public ValueList<NoteId>? Notes { get; init; }

    /// <summary>How far to move them, -127 to 127; 12 is an octave up.</summary>
    [Description("How far to move them, -127 to 127; 12 is an octave up.")]
    [Range(-NoteRules.HighestPitch, NoteRules.HighestPitch)]
    public required int Semitones { get; init; }
}
