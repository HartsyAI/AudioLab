namespace AudioLab.Core.Rules;

/// <summary>One line of an LRC file: the time it starts, in seconds, and its text.</summary>
/// <param name="Time">Where the line starts.</param>
/// <param name="Text">What is sung.</param>
public readonly record struct LrcLine(double Time, string Text);
