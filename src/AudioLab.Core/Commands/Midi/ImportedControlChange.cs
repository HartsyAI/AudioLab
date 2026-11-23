using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Core.Commands;

/// <summary>A controller event carried by <see cref="ImportMidiClip"/>.</summary>
/// <param name="Time">Where the event happens, in seconds from the start of the clip, up to its length.</param>
/// <param name="Controller">The controller number, 0 to 127; 64 is the sustain pedal.</param>
/// <param name="Value">The controller value, 0 to 127; 64 and up presses a pedal.</param>
public readonly record struct ImportedControlChange(
    [property: Description("Where the event happens, in seconds from the start of the clip, up to its length.")]
    [property: Range(0, ProjectLimits.MaxSeconds)]
    double Time,
    [property: Description("The controller number, 0 to 127; 64 is the sustain pedal.")]
    [property: Range(0, NoteRules.HighestPitch)]
    int Controller,
    [property: Description("The controller value, 0 to 127; 64 and up presses a pedal.")]
    [property: Range(0, NoteRules.HighestPitch)]
    int Value);
