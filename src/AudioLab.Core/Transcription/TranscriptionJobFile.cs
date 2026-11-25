using System.Text.Json.Serialization;

namespace AudioLab.Core.Transcription;

/// <summary>The state file of a job (D92): what the job is and where it stands. The chunk log holds the work done.</summary>
/// <param name="Version">The format version; 1.</param>
/// <param name="Owner">The user who owns the file; a restart finds the job by it, and the folder must match.</param>
/// <param name="Media">The media file's id.</param>
/// <param name="Sha256">The hash of the file's bytes the job runs on; a different hash discards the checkpoints.</param>
/// <param name="Model">The model the job runs with.</param>
/// <param name="WindowFrames">The window length in frames at 16 kHz.</param>
/// <param name="StepFrames">The step between windows in frames at 16 kHz.</param>
/// <param name="Chunks">How many windows the job has.</param>
/// <param name="State">Where the job stands.</param>
/// <param name="Reason">Why it failed or was cancelled, when it was; a sentence for the user.</param>
/// <param name="Created">When the job was accepted, in UTC.</param>
/// <param name="Updated">When the state last changed, in UTC.</param>
/// <param name="Duration">The media's length in seconds, so a finished job reports its progress after a restart.</param>
public sealed record TranscriptionJobFile(
    [property: JsonPropertyName("version")] int Version,
    [property: JsonPropertyName("owner")] string Owner,
    [property: JsonPropertyName("media")] string Media,
    [property: JsonPropertyName("sha256")] string Sha256,
    [property: JsonPropertyName("model")] string Model,
    [property: JsonPropertyName("windowFrames")] long WindowFrames,
    [property: JsonPropertyName("stepFrames")] long StepFrames,
    [property: JsonPropertyName("chunks")] int Chunks,
    [property: JsonPropertyName("state")] TranscriptionState State,
    [property: JsonPropertyName("reason")] string? Reason,
    [property: JsonPropertyName("created")] DateTimeOffset Created,
    [property: JsonPropertyName("updated")] DateTimeOffset Updated,
    [property: JsonPropertyName("duration")] double Duration = 0)
{
    /// <summary>The only format version the code writes and reads.</summary>
    public const int CurrentVersion = 1;
}
