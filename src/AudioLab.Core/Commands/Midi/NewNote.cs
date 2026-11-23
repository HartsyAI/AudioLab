using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Core.Commands;

/// <summary>A note to add to a clip: the same values as <see cref="AddNote"/>.</summary>
/// <param name="Pitch">The MIDI pitch, 0 to 127; 60 is middle C.</param>
/// <param name="Start">Where the note starts, in seconds from the start of the clip; it must start inside the clip.</param>
/// <param name="Duration">How long it sounds, in seconds; one grid step, at least 0.05, when absent.</param>
/// <param name="Velocity">How hard it is played, 1 to 127; 100 when absent.</param>
[method: JsonConstructor]
public readonly record struct NewNote(
    [property: Description("The MIDI pitch, 0 to 127; 60 is middle C.")]
    [property: Range(NoteRules.LowestPitch, NoteRules.HighestPitch)]
    int Pitch,
    [property: Description("Where the note starts, in seconds from the start of the clip; it must start inside the clip.")]
    [property: Range(0, ProjectLimits.MaxSeconds)]
    double Start,
    [property: Description("How long it sounds, in seconds; one grid step, at least 0.05, when absent.")]
    [property: Range(NoteRules.MinDuration, ProjectLimits.MaxSeconds)]
    double? Duration = null,
    [property: Description("How hard it is played, 1 to 127; 100 when absent.")]
    [property: Range(1, NoteRules.HighestVelocity)]
    int? Velocity = null);
