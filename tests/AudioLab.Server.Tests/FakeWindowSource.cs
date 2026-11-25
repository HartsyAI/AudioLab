using AudioLab.Core.Model;
using AudioLab.Core.Transcription;
using AudioLab.Server.Transcription;

namespace AudioLab.Server.Tests;

/// <summary>A media file of any length with no samples: the job's windows are counted, not read.</summary>
public sealed class FakeWindowSource(double durationSeconds) : IAudioWindowSource
{
    /// <summary>How many windows the job has read.</summary>
    public int Reads { get; private set; }

    /// <inheritdoc />
    public double DurationOf(MediaRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return durationSeconds;
    }

    /// <inheritdoc />
    public Task<ReadOnlyMemory<float>> ReadAsync(UserId owner, MediaRecord record, TranscriptionWindow window, CancellationToken cancellationToken)
    {
        Reads++;
        return Task.FromResult(ReadOnlyMemory<float>.Empty);
    }
}
