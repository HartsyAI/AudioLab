using System.Buffers;
using AudioLab.Audio;
using AudioLab.Audio.Wav;
using AudioLab.Core.Model;
using AudioLab.Core.Transcription;
using AudioLab.Server.Media;
using AudioLab.Server.Store;

namespace AudioLab.Server.Transcription;

/// <summary>Reads windows of a stored WAV file, mixed to mono and resampled to 16 kHz for the model.</summary>
public sealed class StoredAudioWindowSource(StoreRoot root) : IAudioWindowSource
{
    private const int FramesPerRead = 1 << 16;

    /// <inheritdoc />
    public double DurationOf(MediaRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return record.Duration;
    }

    /// <inheritdoc />
    public async Task<ReadOnlyMemory<float>> ReadAsync(
        UserId owner,
        MediaRecord record,
        TranscriptionWindow window,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);
        await using FileStream stream = MediaStore.OpenForReading(root.Paths.MediaDataFile(owner, record.Id));
        WavHeader format = WavReader.Read(stream);
        long startFrame = (long)Math.Round(window.StartSeconds * format.SampleRate, MidpointRounding.AwayFromZero);
        long wanted = (long)Math.Round(window.LengthSeconds * format.SampleRate, MidpointRounding.AwayFromZero);
        long frames = Math.Max(0, Math.Min(wanted, format.Frames - startFrame));
        if (frames == 0)
        {
            return ReadOnlyMemory<float>.Empty;
        }
        float[] mono = new float[frames];
        byte[] bytes = ArrayPool<byte>.Shared.Rent(FramesPerRead * format.BlockAlign);
        float[] samples = ArrayPool<float>.Shared.Rent(FramesPerRead * format.Channels);
        try
        {
            stream.Position = format.DataOffset + (startFrame * format.BlockAlign);
            long done = 0;
            while (done < frames)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int count = (int)Math.Min(FramesPerRead, frames - done);
                int byteCount = count * format.BlockAlign;
                await stream.ReadExactlyAsync(bytes.AsMemory(0, byteCount), cancellationToken);
                WavSamples.ToFloat(bytes.AsSpan(0, byteCount), format.Encoding, samples);
                for (int frame = 0; frame < count; frame++)
                {
                    float sum = 0;
                    for (int channel = 0; channel < format.Channels; channel++)
                    {
                        sum += samples[(frame * format.Channels) + channel];
                    }
                    mono[done + frame] = sum / format.Channels;
                }
                done += count;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(bytes);
            ArrayPool<float>.Shared.Return(samples);
        }
        PcmAudio resampled = Resampler.ToRate(new PcmAudio(format.SampleRate, [mono]), TranscriptionWindows.SampleRate, cancellationToken);
        return resampled.Channel(0).ToArray();
    }
}
