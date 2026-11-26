using AudioLab.Core.Model;
using AudioLab.Core.Transcription;
using AudioLab.Server.Transcription;

namespace AudioLab.Server.Tests;

/// <summary>A media file whose windows cannot be read: each read fails with the exception given. With <paramref name="onDuration"/>, the duration
/// read fails instead.</summary>
public sealed class BrokenWindowSource(double durationSeconds, Exception failure, bool onDuration = false) : IAudioWindowSource
{
    /// <inheritdoc />
    public double DurationOf(MediaRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (onDuration)
        {
            throw failure;
        }
        return durationSeconds;
    }

    /// <inheritdoc />
    public Task<ReadOnlyMemory<float>> ReadAsync(UserId owner, MediaRecord record, TranscriptionWindow window, CancellationToken cancellationToken)
    {
        return Task.FromException<ReadOnlyMemory<float>>(failure);
    }
}
