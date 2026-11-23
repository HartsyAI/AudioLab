using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using AudioLab.Audio;
using AudioLab.Audio.Wav;
using AudioLab.Core.Model;
using AudioLab.Server.Media;
using AudioLab.Server.Store;

namespace AudioLab.Server.Render;

/// <summary>Supplies the decoded audio of one user's media files to the engine, at the render's rate; a file of anyone else is not there.</summary>
/// <remarks>
/// It is made for one user and one render and never shared, so ownership is the media store's own scoping and nothing outlives the render.
/// Each distinct file is decoded and converted once and remembered, so a project that plays two files in two hundred clips holds two
/// copies, and the total is held to <see cref="RenderOptions.MaxDecodedBytesPerRender"/>; a file that would break a limit is refused before
/// anything is allocated for it. The engine asks while it prepares a plan, on the thread that prepares it, so the reads are synchronous and
/// the token stops a long decode or conversion; the caller must run the plan on a task of its own, never on a request's thread. A file that
/// is not the user's or is gone is not available; one whose bytes are missing or no longer match its record is refused for good, as the
/// peaks rebuild does (D37), and is not available; a folder or file that cannot be read is an <see cref="IOException"/>, never a missing file.
/// </remarks>
public sealed class UserMediaSource(
    MediaStore store,
    UserId user,
    int sampleRate,
    RenderOptions options,
    ILogger logger,
    CancellationToken cancellationToken = default) : IMediaSource
{
    private const int BlockFrames = 8192;
    private const int KeptChannels = 2;

    private readonly Dictionary<MediaId, PcmAudio> _supplied = [];
    private long _held;

    /// <summary>How many files were decoded: each distinct file of the render once.</summary>
    public int DecodedFiles => _supplied.Count;

    /// <summary>How many bytes of audio the render holds.</summary>
    public long HeldBytes => _held;

    /// <inheritdoc />
    /// <exception cref="RenderLimitException">The file would take more than a limit allows.</exception>
    /// <exception cref="IOException">The user's media folder or the file cannot be read.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled while the file was being read or converted.</exception>
    public bool TryGet(MediaId id, [NotNullWhen(true)] out PcmAudio? audio)
    {
        if (_supplied.TryGetValue(id, out audio))
        {
            return true;
        }
        MediaRecord record;
        try
        {
            record = store.Get(user, id);
        }
        catch (MediaNotFoundException)
        {
            audio = null;
            return false;
        }
        audio = Supply(id, record);
        return audio is not null;
    }

    private PcmAudio? Supply(MediaId id, MediaRecord record)
    {
        try
        {
            using FileStream stream = store.OpenContent(user, id, asynchronous: false);
            WavHeader format = WavReader.Read(stream);
            if (stream.Length != record.Bytes || !Matches(record, format))
            {
                return Refuse(id, "its bytes no longer match its record");
            }
            PcmAudio converted = Convert(stream, format);
            _supplied[id] = converted;
            return converted;
        }
        catch (MediaNotFoundException)
        {
            // Bytes that are not there while the record is, and no delete under way, are a damaged file; a delete is not.
            return store.IsDeleting(user, id) ? null : Refuse(id, "its bytes are missing");
        }
        catch (Exception exception) when (exception is WavFormatException or EndOfStreamException)
        {
            return Refuse(id, "its bytes are not the WAV file that was uploaded");
        }
        catch (UnauthorizedAccessException exception)
        {
            throw new IOException("A media file cannot be opened.", exception);
        }
    }

    private PcmAudio? Refuse(MediaId id, string reason)
    {
        RenderLog.MediaUnusable(logger, StorePaths.MediaFileName(id), reason);
        store.Refuse(user, id, reason);
        return null;
    }

    private static bool Matches(MediaRecord record, WavHeader format)
    {
        return format.Encoding == record.Encoding && format.Channels == record.Channels && format.SampleRate == record.SampleRate;
    }

    private PcmAudio Convert(FileStream stream, WavHeader format)
    {
        int kept = Math.Min(format.Channels, KeptChannels);
        long frames = format.Frames;
        long renderFrames = RenderFrames(frames, format.SampleRate);
        long nativeBytes = frames * kept * sizeof(float);
        long renderBytes = renderFrames * kept * sizeof(float);
        if (frames > Array.MaxLength || nativeBytes > options.MaxDecodedBytesPerFile || renderBytes > options.MaxDecodedBytesPerFile)
        {
            throw new RenderLimitException(
                $"A media file is too long to render: it would take more than {options.MaxDecodedBytesPerFile} bytes decoded at {sampleRate} Hz.");
        }
        if (_held + renderBytes > options.MaxDecodedBytesPerRender)
        {
            throw new RenderLimitException(
                $"The media of the project would take more than {options.MaxDecodedBytesPerRender} bytes decoded to render.");
        }
        PcmAudio native = Decode(stream, format, kept);
        PcmAudio converted = Resampler.ToRate(native, sampleRate, cancellationToken);
        _held += (long)converted.Frames * kept * sizeof(float);
        return converted;
    }

    private long RenderFrames(long frames, int fileRate)
    {
        try
        {
            return Resampler.OutputFrames(frames, fileRate, sampleRate);
        }
        catch (ArgumentOutOfRangeException)
        {
            throw new RenderLimitException($"A media file is too long to render at {sampleRate} Hz.");
        }
    }

    private PcmAudio Decode(FileStream stream, WavHeader format, int kept)
    {
        long frames = format.Frames;
        float[][] channels = new float[kept][];
        for (int channel = 0; channel < kept; channel++)
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
                for (int channel = 0; channel < kept; channel++)
                {
                    float[] target = channels[channel];
                    for (int frame = 0; frame < count; frame++)
                    {
                        target[done + frame] = samples[(frame * format.Channels) + channel];
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
