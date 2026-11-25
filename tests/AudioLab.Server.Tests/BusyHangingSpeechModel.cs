using AudioLab.Core.Transcription;

namespace AudioLab.Server.Tests;

/// <summary>A speech model that never answers and says it is busy: each call waits until it is cancelled.</summary>
public sealed class BusyHangingSpeechModel : ISpeechModel
{
    /// <inheritdoc />
    public string ModelId => SpeechModelCatalog.DefaultSpeechModel;

    /// <inheritdoc />
    public bool IsBusy => true;

    /// <inheritdoc />
    public async Task<IReadOnlyList<SpeechSegment>> TranscribeAsync(SpeechWindow window, CancellationToken cancellationToken)
    {
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        return [];
    }
}
