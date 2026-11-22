using System.Text.Json.Serialization;

namespace AudioLab.Core.Model;

/// <summary>One lyric word with its time in the clip's source audio.</summary>
/// <param name="Word">The word as written.</param>
/// <param name="Start">Where it starts, in seconds of the clip's source audio.</param>
/// <param name="End">Where it ends, in seconds of the clip's source audio.</param>
/// <param name="Line">The index of the lyric line the word belongs to.</param>
/// <param name="Unsure">True when the aligner probably invented the time; null otherwise.</param>
public readonly record struct LyricWord(
    [property: JsonPropertyName("w")] string Word,
    [property: JsonPropertyName("s")] double Start,
    [property: JsonPropertyName("e")] double End,
    [property: JsonPropertyName("line")] int? Line = null,
    [property: JsonPropertyName("u")] bool? Unsure = null);
