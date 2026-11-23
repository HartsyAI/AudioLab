using AudioLab.Audio.Wav;

namespace AudioLab.Audio.Live;

/// <summary>Turns a WAV file into float samples with <see cref="WavReader"/> and <see cref="WavSamples"/>, which the server uses too (D37).</summary>
/// <remarks>The header check and the scan of a float file for samples that are not numbers run at once; the conversion runs in slices.</remarks>
public static class ClipDecoder
{
    private const int SliceFrames = 16384;

    /// <summary>Decodes <paramref name="file"/> to one array of samples for each channel, calling <paramref name="pause"/> between slices.</summary>
    /// <exception cref="WavFormatException">The file is not a WAV file the reader accepts, or holds more frames than an array can.</exception>
    public static async Task<PcmAudio> DecodeAsync(byte[] file, Func<CancellationToken, Task> pause, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(pause);
        using MemoryStream stream = new(file, writable: false);
        WavHeader header = WavReader.Read(stream);
        await WavReader.RequireFiniteAsync(stream, header, cancellationToken);
        if (header.Frames > Array.MaxLength)
        {
            throw new WavFormatException("The file is longer than the page can hold");
        }
        int frames = (int)header.Frames;
        int channels = header.Channels;
        int width = WavSamples.BytesPerSample(header.Encoding);
        float[][] samples = new float[channels][];
        for (int channel = 0; channel < channels; channel++)
        {
            samples[channel] = new float[frames];
        }
        float[] scratch = new float[SliceFrames * channels];
        for (int at = 0; at < frames; at += SliceFrames)
        {
            int count = Math.Min(SliceFrames, frames - at);
            long offset = header.DataOffset + ((long)at * header.BlockAlign);
            WavSamples.ToFloat(file.AsSpan((int)offset, count * channels * width), header.Encoding, scratch);
            for (int channel = 0; channel < channels; channel++)
            {
                float[] destination = samples[channel];
                for (int frame = 0; frame < count; frame++)
                {
                    destination[at + frame] = scratch[(frame * channels) + channel];
                }
            }
            await pause(cancellationToken);
        }
        return new PcmAudio(header.SampleRate, samples);
    }
}
