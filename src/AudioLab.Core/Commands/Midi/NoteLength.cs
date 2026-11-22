using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Core.Commands;

/// <summary>How long a note is: its new length, absolute.</summary>
/// <param name="Note">The note to resize.</param>
/// <param name="Duration">How long it sounds, in seconds, at least 0.02.</param>
public readonly record struct NoteLength(
    [property: Description("The note to resize.")] NoteId Note,
    [property: Description("How long it sounds, in seconds, at least 0.02.")]
    [property: Range(NoteRules.MinDuration, ProjectLimits.MaxSeconds)]
    double Duration);
