using System.Text.Json.Serialization;
using AudioLab.Core.Model;

namespace AudioLab.Core.Transcription;

/// <summary>One segment of a transcript, with times in seconds of the media file.</summary>
/// <param name="Start">Where the segment starts, in seconds of the media.</param>
/// <param name="End">Where it ends, in seconds of the media.</param>
/// <param name="Text">The text as the model wrote it.</param>
/// <param name="Kind">Whether the words are spoken, sung or something else.</param>
/// <param name="Words">The word times, when the model gives them.</param>
public sealed record TranscriptSegment(
    [property: JsonPropertyName("start")] double Start,
    [property: JsonPropertyName("end")] double End,
    [property: JsonPropertyName("text")] string Text,
    [property: JsonPropertyName("kind")] SpeechKind Kind,
    [property: JsonPropertyName("words")]
    [property: JsonConverter(typeof(ValueListJsonConverter<LyricWord>))]
    ValueList<LyricWord>? Words = null);
