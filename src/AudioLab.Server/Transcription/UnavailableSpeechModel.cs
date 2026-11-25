using AudioLab.Core.Transcription;

namespace AudioLab.Server.Transcription;

/// <summary>The speech model until the engine serves one: every window fails, so no answer is ever invented (D98).</summary>
public sealed class UnavailableSpeechModel : ISpeechModel
{
    /// <inheritdoc />
    public string ModelId => "unavailable";

    /// <inheritdoc />
    public Task<IReadOnlyList<SpeechSegment>> TranscribeAsync(SpeechWindow window, CancellationToken cancellationToken)
    {
        return Task.FromException<IReadOnlyList<SpeechSegment>>(new SpeechModelException("The speech model is not installed.", retryable: false));
    }
}
