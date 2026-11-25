namespace AudioLab.Web.Components.Lyrics;

/// <summary>A word-time edit a row asks for: the word, the start and the end in source seconds (null where kept), and what held it.</summary>
/// <param name="Word">The word's index.</param>
/// <param name="Start">The start in seconds of the source audio, or null to keep it.</param>
/// <param name="End">The end in seconds of the source audio, or null to keep it.</param>
/// <param name="Held">What the neighbours held, as text for the user, or null when nothing was held.</param>
public readonly record struct LyricWordEdit(int Word, double? Start, double? End, string? Held = null);
