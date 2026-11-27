using AudioLab.Core.Model;
using AudioLab.Core.Transcription;

namespace AudioLab.Server.Transcription;

/// <summary>Gives a transcription job the length of a media file and one window of it at a time (D96).</summary>
/// <remarks>The seam that lets a test run a job of many hours without holding its audio in memory.</remarks>
public interface IAudioWindowSource
{
    /// <summary>The length of the file in seconds.</summary>
    public double DurationOf(MediaRecord record);

    /// <summary>The window's audio as mono samples at <see cref="TranscriptionWindows.SampleRate"/>.</summary>
    /// <exception cref="IOException">The file's bytes cannot be read.</exception>
    public Task<ReadOnlyMemory<float>> ReadAsync(UserId owner, MediaRecord record, TranscriptionWindow window, CancellationToken cancellationToken);
}
