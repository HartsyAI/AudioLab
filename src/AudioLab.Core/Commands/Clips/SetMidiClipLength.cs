using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>Sets the length of a MIDI clip; refused if a note starts at or after the new end, or a controller event falls past it.</summary>
public sealed record SetMidiClipLength : Command
{
    /// <summary>The MIDI clip to edit.</summary>
    [Description("The MIDI clip to edit.")]
    public required ClipId Clip { get; init; }

    /// <summary>The new length, in seconds, at least 0.05.</summary>
    [Description("The new length, in seconds, at least 0.05.")]
    [Range(MidiClip.MinLength, ProjectLimits.MaxSeconds)]
    public required double Length { get; init; }
}
