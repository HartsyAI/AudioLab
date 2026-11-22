using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>Makes notes longer or shorter by the same amount, to at least 0.02 seconds.</summary>
public sealed record ResizeNotes : Command
{
    /// <summary>The MIDI clip the notes belong to.</summary>
    [Description("The MIDI clip the notes belong to.")]
    public required ClipId Clip { get; init; }

    /// <summary>The notes to resize; every note of the clip when absent.</summary>
    [Description("The notes to resize; every note of the clip when absent.")]
    [JsonConverter(typeof(ValueListJsonConverter<NoteId>))]
    public ValueList<NoteId>? Notes { get; init; }

    /// <summary>How much to add to each note's length, in seconds, from -1000000 to 1000000; negative shortens it.</summary>
    [Description("How much to add to each note's length, in seconds, from -1000000 to 1000000; negative shortens it.")]
    [Range(-ProjectLimits.MaxSeconds, ProjectLimits.MaxSeconds)]
    public required double Duration { get; init; }
}
