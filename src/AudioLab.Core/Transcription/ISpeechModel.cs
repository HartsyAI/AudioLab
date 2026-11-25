namespace AudioLab.Core.Transcription;

/// <summary>A speech model that turns one window of audio into segments (D98). The engine implements it through HartsyInference.</summary>
public interface ISpeechModel
{
    /// <summary>The model's name, stored with each job, so a changed model never mixes answers into one transcript.</summary>
    public string ModelId { get; }

    /// <summary>Transcribes one window.</summary>
    /// <param name="window">The audio of the window.</param>
    /// <param name="cancellationToken">Cancels the call; a cancelled call keeps nothing.</param>
    /// <exception cref="SpeechModelException">The model could not answer; its Retryable says if a retry may work.</exception>
    public Task<IReadOnlyList<SpeechSegment>> TranscribeAsync(SpeechWindow window, CancellationToken cancellationToken);
}
