using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Core.Commands;

/// <summary>Sets the velocity of notes.</summary>
public sealed record SetNoteVelocity : Command
{
    /// <summary>The MIDI clip the notes belong to.</summary>
    [Description("The MIDI clip the notes belong to.")]
    public required ClipId Clip { get; init; }

    /// <summary>The notes to set; every note of the clip when absent.</summary>
    [Description("The notes to set; every note of the clip when absent.")]
    [JsonConverter(typeof(ValueListJsonConverter<NoteId>))]
    public ValueList<NoteId>? Notes { get; init; }

    /// <summary>How hard they are played, 1 to 127.</summary>
    [Description("How hard they are played, 1 to 127.")]
    [Range(1, NoteRules.HighestPitch)]
    public required int Velocity { get; init; }
}
