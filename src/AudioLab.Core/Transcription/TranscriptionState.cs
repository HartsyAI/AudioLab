using System.Text.Json.Serialization;

namespace AudioLab.Core.Transcription;

/// <summary>Where a transcription job is (D90).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<TranscriptionState>))]
public enum TranscriptionState
{
    /// <summary>Accepted and waiting for a place to run.</summary>
    [JsonStringEnumMemberName("queued")]
    Queued,

    /// <summary>A worker is taking its chunks.</summary>
    [JsonStringEnumMemberName("running")]
    Running,

    /// <summary>The server stopped while the job ran; it is queued again when the server starts.</summary>
    [JsonStringEnumMemberName("paused")]
    Paused,

    /// <summary>The user stopped it at a chunk boundary; resume continues it.</summary>
    [JsonStringEnumMemberName("cancelled")]
    Cancelled,

    /// <summary>A chunk failed in a way a retry does not fix; resume tries again.</summary>
    [JsonStringEnumMemberName("failed")]
    Failed,

    /// <summary>The transcript is stored and the checkpoints are gone.</summary>
    [JsonStringEnumMemberName("done")]
    Done,
}
