using System.Text.Json.Serialization;

namespace AudioLab.Core.Transcription;

/// <summary>What a client is told about a media file's transcription (D97). It names no user.</summary>
/// <param name="Media">The media file's id.</param>
/// <param name="State">Where the job stands.</param>
/// <param name="ChunksDone">How many windows are finished.</param>
/// <param name="Chunks">How many windows the job has.</param>
/// <param name="ReachedSeconds">How far into the media the finished windows reach, in seconds.</param>
/// <param name="CancelRequested">Whether a cancel is waiting for the next chunk boundary.</param>
/// <param name="Reason">Why it failed or was cancelled, when it was.</param>
public sealed record TranscriptionStatus(
    [property: JsonPropertyName("media")] string Media,
    [property: JsonPropertyName("state")] TranscriptionState State,
    [property: JsonPropertyName("chunksDone")] int ChunksDone,
    [property: JsonPropertyName("chunks")] int Chunks,
    [property: JsonPropertyName("reachedSeconds")] double ReachedSeconds,
    [property: JsonPropertyName("cancelRequested")] bool CancelRequested,
    [property: JsonPropertyName("reason")] string? Reason);
