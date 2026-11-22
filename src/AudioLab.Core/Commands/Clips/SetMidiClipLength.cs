using System.ComponentModel;
using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>Sets the length of a MIDI clip; refused if a note or controller event would fall outside it.</summary>
public sealed record SetMidiClipLength : Command
{
    /// <summary>The MIDI clip to edit.</summary>
    [Description("The MIDI clip to edit.")]
    public required ClipId Clip { get; init; }

    /// <summary>The new length, in seconds; a length below 0.05 becomes 0.05.</summary>
    [Description("The new length, in seconds; a length below 0.05 becomes 0.05.")]
    public required double Length { get; init; }
}
