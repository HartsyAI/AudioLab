namespace AudioLab.Core.Rules;

/// <summary>A word as an aligner or a speech model reports it, in seconds from the start of the audio it was sent.</summary>
/// <param name="Word">The word.</param>
/// <param name="Start">Where it starts.</param>
/// <param name="End">Where it ends.</param>
public readonly record struct TimedWord(string? Word, double Start, double End);
