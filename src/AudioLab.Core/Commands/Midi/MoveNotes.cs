using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Core.Commands;

/// <summary>Moves notes in time and pitch together; each stays inside the clip and the pitch range.</summary>
public sealed record MoveNotes : Command
{
    /// <summary>The MIDI clip the notes belong to.</summary>
    [Description("The MIDI clip the notes belong to.")]
    public required ClipId Clip { get; init; }

    /// <summary>The notes to move; every note of the clip when absent.</summary>
    [Description("The notes to move; every note of the clip when absent.")]
    [JsonConverter(typeof(ValueListJsonConverter<NoteId>))]
    public ValueList<NoteId>? Notes { get; init; }

    /// <summary>How far to move them in time, in seconds, from -1000000 to 1000000; negative moves them earlier.</summary>
    [Description("How far to move them in time, in seconds, from -1000000 to 1000000; negative moves them earlier.")]
    [Range(-ProjectLimits.MaxSeconds, ProjectLimits.MaxSeconds)]
    public required double Time { get; init; }

    /// <summary>How far to move them in pitch, in semitones, from -127 to 127; negative moves them down.</summary>
    [Description("How far to move them in pitch, in semitones, from -127 to 127; negative moves them down.")]
    [Range(-NoteRules.HighestPitch, NoteRules.HighestPitch)]
    public required int Pitch { get; init; }
}
