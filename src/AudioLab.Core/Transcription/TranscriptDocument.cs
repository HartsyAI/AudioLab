using System.Text.Json.Serialization;
using AudioLab.Core.Model;

namespace AudioLab.Core.Transcription;

/// <summary>The stored transcript of one media file (D94): its segments in order, with times in seconds of the file.</summary>
/// <param name="Version">The format version; 1.</param>
/// <param name="Media">The media file's id.</param>
/// <param name="Sha256">The hash of the file's bytes that were transcribed.</param>
/// <param name="Model">The model that transcribed it.</param>
/// <param name="Duration">The length of the file in seconds.</param>
/// <param name="Segments">The segments, in order.</param>
public sealed record TranscriptDocument(
    [property: JsonPropertyName("version")] int Version,
    [property: JsonPropertyName("media")] string Media,
    [property: JsonPropertyName("sha256")] string Sha256,
    [property: JsonPropertyName("model")] string Model,
    [property: JsonPropertyName("duration")] double Duration,
    [property: JsonPropertyName("segments")]
    [property: JsonConverter(typeof(ValueListJsonConverter<TranscriptSegment>))]
    ValueList<TranscriptSegment> Segments)
{
    /// <summary>The only format version the code writes and reads.</summary>
    public const int CurrentVersion = 1;
}
