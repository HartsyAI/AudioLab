using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Core.Commands;

/// <summary>Raises or lowers the velocity of notes by an amount, each kept from 1 to 127.</summary>
public sealed record AdjustNoteVelocity : Command
{
    /// <summary>The MIDI clip the notes belong to.</summary>
    [Description("The MIDI clip the notes belong to.")]
    public required ClipId Clip { get; init; }

    /// <summary>The notes to change, from 1 to 1000000; every note of the clip when absent.</summary>
    [Description("The notes to change, from 1 to 1000000; every note of the clip when absent.")]
    [JsonConverter(typeof(ValueListJsonConverter<NoteId>))]
    [MinLength(1)]
    [MaxLength(ProjectLimits.MaxListItems)]
    public ValueList<NoteId>? Notes { get; init; }

    /// <summary>How much to add to each velocity, from -126 to 126; a velocity stops at 1 and at 127.</summary>
    [Description("How much to add to each velocity, from -126 to 126; a velocity stops at 1 and at 127.")]
    [Range(-(NoteRules.HighestVelocity - 1), NoteRules.HighestVelocity - 1)]
    public required int Amount { get; init; }
}
