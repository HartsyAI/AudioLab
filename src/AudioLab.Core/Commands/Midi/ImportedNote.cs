using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Core.Commands;

/// <summary>A note carried by <see cref="ImportMidiClip"/>: no id, which the server gives it in the order the notes come.</summary>
/// <param name="Pitch">The MIDI pitch, 0 to 127; 60 is middle C.</param>
/// <param name="Start">Where the note starts, in seconds from the start of the clip; it must start inside the clip.</param>
/// <param name="Duration">How long it sounds, in seconds, at least 0.02.</param>
/// <param name="Velocity">How hard it is played, 1 to 127.</param>
public readonly record struct ImportedNote(
    [property: Description("The MIDI pitch, 0 to 127; 60 is middle C.")]
    [property: Range(NoteRules.LowestPitch, NoteRules.HighestPitch)]
    int Pitch,
    [property: Description("Where the note starts, in seconds from the start of the clip; it must start inside the clip.")]
    [property: Range(0, ProjectLimits.MaxSeconds)]
    double Start,
    [property: Description("How long it sounds, in seconds, at least 0.02.")]
    [property: Range(NoteRules.MinDuration, ProjectLimits.MaxSeconds)]
    double Duration,
    [property: Description("How hard it is played, 1 to 127.")]
    [property: Range(1, NoteRules.HighestPitch)]
    int Velocity);
