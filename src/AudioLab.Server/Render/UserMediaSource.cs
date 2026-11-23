using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using AudioLab.Audio;
using AudioLab.Core.Model;
using AudioLab.Server.Media;
using AudioLab.Server.Store;

namespace AudioLab.Server.Render;

/// <summary>Supplies the decoded audio of one user's media files to the engine; a file of anyone else is simply not there.</summary>
/// <remarks>
/// It is made for one user and one render and never shared, so ownership is the media store's own scoping and no cache outlives
/// the files it was filled from. The engine asks while it prepares a plan, on the thread that prepares it, so the reads are
/// synchronous and the token stops a long decode between blocks. A file that is not the user's, is gone, or no longer matches
/// its record is not available; a folder that cannot be read is an <see cref="IOException"/>, so it is never taken for a missing file.
/// </remarks>
public sealed class UserMediaSource(
    MediaStore store,
    UserId user,
    long maxDecodedBytes,
    ILogger logger,
    CancellationToken cancellationToken = default) : IMediaSource
{
    private const int BlockFrames = 8192;

    /// <inheritdoc />
    /// <exception cref="RenderLimitException">The file would decode to more than the limit allows.</exception>
    /// <exception cref="IOException">The user's media folder cannot be read.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled while the file was being read.</exception>
    public bool TryGet(MediaId id, [NotNullWhen(true)] out PcmAudio? audio)
    {
        audio = null;
        MediaRecord record;
        try
        {
            record = store.Get(user, id);
        }
        catch (MediaNotFoundException)
        {
            return false;
        }
        try
        {
            using FileStream stream = store.OpenContent(user, id);
            Media.WavFormat format = WavReader.Read(stream);
            if (!Matches(record, format, stream.Length))
            {
                RenderLog.MediaUnusable(logger, StorePaths.MediaFileName(id), "its bytes no longer match its record");
                return false;
            }
            audio = Decode(stream, format);
            return true;
        }
        catch (Exception exception) when (exception is MediaNotFoundException or MediaRejectedException or EndOfStreamException)
        {
            RenderLog.MediaUnusable(logger, StorePaths.MediaFileName(id), exception.GetType().Name);
            return false;
        }
    }

    private static bool Matches(MediaRecord record, Media.WavFormat format, long length)
    {
        return length == record.Bytes
            && format.Encoding == record.Encoding
            && format.Channels == record.Channels
            && format.SampleRate == record.SampleRate;
    }

    private PcmAudio Decode(FileStream stream, Media.WavFormat format)
    {
        long frames = format.Frames;
        if (frames > Array.MaxLength || frames * format.Channels * sizeof(float) > maxDecodedBytes)
        {
            throw new RenderLimitException($"A media file is too long to render: it would take more than {maxDecodedBytes} bytes decoded.");
        }
        float[][] channels = new float[format.Channels][];
        for (int channel = 0; channel < channels.Length; channel++)
        {
            channels[channel] = new float[frames];
        }
        byte[] bytes = ArrayPool<byte>.Shared.Rent(BlockFrames * format.BlockAlign);
        float[] samples = ArrayPool<float>.Shared.Rent(BlockFrames * format.Channels);
        try
        {
            stream.Position = format.DataOffset;
            for (long done = 0; done < frames; done += BlockFrames)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int count = (int)Math.Min(BlockFrames, frames - done);
                stream.ReadExactly(bytes.AsSpan(0, count * format.BlockAlign));
                WavSamples.ToFloat(bytes.AsSpan(0, count * format.BlockAlign), format.Encoding, samples);
                for (int channel = 0; channel < channels.Length; channel++)
                {
                    float[] target = channels[channel];
                    for (int frame = 0; frame < count; frame++)
                    {
                        target[done + frame] = samples[(frame * channels.Length) + channel];
                    }
                }
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(bytes);
            ArrayPool<float>.Shared.Return(samples);
        }
        return new PcmAudio(format.SampleRate, channels);
    }
}
