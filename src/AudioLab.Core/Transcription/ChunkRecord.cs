using System.Text.Json.Serialization;
using AudioLab.Core.Model;

namespace AudioLab.Core.Transcription;

/// <summary>One line of a job's chunk log (D92): a window's index and the segments it owns, in media seconds.</summary>
/// <param name="Chunk">The window's index.</param>
/// <param name="Segments">The segments the window owns.</param>
public sealed record ChunkRecord(
    [property: JsonPropertyName("chunk")] int Chunk,
    [property: JsonPropertyName("segments")]
    [property: JsonConverter(typeof(ValueListJsonConverter<TranscriptSegment>))]
    ValueList<TranscriptSegment> Segments);
