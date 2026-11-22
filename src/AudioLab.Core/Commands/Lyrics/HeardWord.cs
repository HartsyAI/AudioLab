using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>A word as a model heard it.</summary>
/// <param name="Word">The word, up to 200 characters.</param>
/// <param name="Start">Where it starts, in seconds from the start of the audio that was sent.</param>
/// <param name="End">Where it ends, in seconds from the start of the audio that was sent.</param>
public readonly record struct HeardWord(
    [property: Description("The word, up to 200 characters.")]
    [property: MaxLength(ProjectLimits.MaxName)]
    string Word,
    [property: Description("Where it starts, in seconds from the start of the audio that was sent.")]
    [property: Range(0, ProjectLimits.MaxSeconds)]
    double Start,
    [property: Description("Where it ends, in seconds from the start of the audio that was sent.")]
    [property: Range(0, ProjectLimits.MaxSeconds)]
    double End);
