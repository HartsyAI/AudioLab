using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Core.Commands;

/// <summary>Adds a note to a MIDI clip; the server allocates the note id, which counts up inside the clip.</summary>
public sealed record AddNote : Command
{
    /// <summary>The MIDI clip to add the note to.</summary>
    [Description("The MIDI clip to add the note to.")]
    public required ClipId Clip { get; init; }

    /// <summary>The MIDI pitch, 0 to 127; 60 is middle C.</summary>
    [Description("The MIDI pitch, 0 to 127; 60 is middle C.")]
    [Range(NoteRules.LowestPitch, NoteRules.HighestPitch)]
    public required int Pitch { get; init; }

    /// <summary>Where the note starts, in seconds from the start of the clip; it must start inside the clip.</summary>
    [Description("Where the note starts, in seconds from the start of the clip; it must start inside the clip.")]
    [Range(0, ProjectLimits.MaxSeconds)]
    public required double Start { get; init; }

    /// <summary>How long it sounds, in seconds; one grid step, at least 0.05, when absent.</summary>
    [Description("How long it sounds, in seconds; one grid step, at least 0.05, when absent.")]
    [Range(NoteRules.MinDuration, ProjectLimits.MaxSeconds)]
    public double? Duration { get; init; }

    /// <summary>How hard it is played, 1 to 127; 100 when absent.</summary>
    [Description("How hard it is played, 1 to 127; 100 when absent.")]
    [Range(1, NoteRules.HighestPitch)]
    public int? Velocity { get; init; }
}
