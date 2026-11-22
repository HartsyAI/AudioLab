using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Core.Commands;

/// <summary>Adds a controller event, such as a sustain pedal press, to a MIDI clip.</summary>
public sealed record AddControlChange : Command
{
    /// <summary>The MIDI clip to add the event to.</summary>
    [Description("The MIDI clip to add the event to.")]
    public required ClipId Clip { get; init; }

    /// <summary>Where the event happens, in seconds from the start of the clip, up to its length.</summary>
    [Description("Where the event happens, in seconds from the start of the clip, up to its length.")]
    [Range(0, ProjectLimits.MaxSeconds)]
    public required double Time { get; init; }

    /// <summary>The controller number, 0 to 127; 64 is the sustain pedal.</summary>
    [Description("The controller number, 0 to 127; 64 is the sustain pedal.")]
    [Range(0, NoteRules.HighestPitch)]
    public required int Controller { get; init; }

    /// <summary>The controller value, 0 to 127; 64 and up presses a pedal.</summary>
    [Description("The controller value, 0 to 127; 64 and up presses a pedal.")]
    [Range(0, NoteRules.HighestPitch)]
    public required int Value { get; init; }
}
