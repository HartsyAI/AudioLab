namespace AudioLab.Web.Components.Lyrics;

/// <summary>A word-time edit a row asks for: the word's index, and the start, the end or both as absolute seconds, null where kept.</summary>
/// <param name="Word">The word's index.</param>
/// <param name="Start">The start in seconds of the source audio, or null to keep it.</param>
/// <param name="End">The end in seconds of the source audio, or null to keep it.</param>
public readonly record struct LyricWordEdit(int Word, double? Start, double? End);
