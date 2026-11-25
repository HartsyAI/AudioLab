using AudioLab.Core.Transcription;

namespace AudioLab.Server.Tests;

/// <summary>A speech model that never answers: each call waits until it is cancelled. Counts its calls.</summary>
public sealed class HangingSpeechModel : ISpeechModel
{
    /// <summary>How many calls the model has been asked.</summary>
    public int Calls { get; private set; }

    /// <inheritdoc />
    public string ModelId => "hanging-model";

    /// <inheritdoc />
    public async Task<IReadOnlyList<SpeechSegment>> TranscribeAsync(SpeechWindow window, CancellationToken cancellationToken)
    {
        Calls++;
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        return [];
    }
}
