using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Core.Commands;

/// <summary>Where a note goes: its new start and pitch, absolute.</summary>
/// <param name="Note">The note to move.</param>
/// <param name="Start">Where it starts, in seconds from the start of its clip; kept at least 0.02 seconds before the clip ends.</param>
/// <param name="Pitch">Its pitch, 0 to 127.</param>
public readonly record struct NoteTarget(
    [property: Description("The note to move.")] NoteId Note,
    [property: Description("Where it starts, in seconds from the start of its clip; kept at least 0.02 seconds before the clip ends.")]
    [property: Range(0, ProjectLimits.MaxSeconds)]
    double Start,
    [property: Description("Its pitch, 0 to 127.")]
    [property: Range(NoteRules.LowestPitch, NoteRules.HighestPitch)]
    int Pitch);
