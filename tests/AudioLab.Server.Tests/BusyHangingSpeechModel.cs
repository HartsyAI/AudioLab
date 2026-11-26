using AudioLab.Core.Transcription;

namespace AudioLab.Server.Tests;

/// <summary>A speech model that never answers, and stays busy once asked, as a native call that never returns.</summary>
public sealed class BusyHangingSpeechModel : ISpeechModel
{
    /// <summary>How many calls the model has been asked.</summary>
    public int Calls { get; private set; }

    /// <inheritdoc />
    public string ModelId => SpeechModelCatalog.DefaultSpeechModel;

    /// <inheritdoc />
    public bool IsBusy => Calls > 0;

    /// <inheritdoc />
    public async Task<IReadOnlyList<SpeechSegment>> TranscribeAsync(SpeechWindow window, CancellationToken cancellationToken)
    {
        Calls++;
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        return [];
    }
}
