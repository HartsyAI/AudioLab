using System.Buffers;
using AudioLab.Audio.Wav;
using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Server.Media;

/// <summary>Reads the samples of a checked WAV file once and builds its peaks.</summary>
public static class PeakComputer
{
    private const int FramesPerRead = 16384;

    /// <summary>The peaks of the file's samples at every zoom level.</summary>
    /// <param name="stream">A seekable stream of the whole file, which <paramref name="format"/> was read from.</param>
    /// <param name="format">The header the file was accepted with.</param>
    /// <param name="cancellationToken">Cancels the scan.</param>
    public static async Task<PeakData> ComputeAsync(Stream stream, WavHeader format, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);
        PeakBuilder builder = new(format.Channels, format.SampleRate);
        byte[] bytes = ArrayPool<byte>.Shared.Rent(FramesPerRead * format.BlockAlign);
        float[] samples = ArrayPool<float>.Shared.Rent(FramesPerRead * format.Channels);
        try
        {
            stream.Position = format.DataOffset;
            long remaining = format.Frames;
            while (remaining > 0)
            {
                int frames = (int)Math.Min(FramesPerRead, remaining);
                int byteCount = frames * format.BlockAlign;
                await stream.ReadExactlyAsync(bytes.AsMemory(0, byteCount), cancellationToken);
                WavSamples.ToFloat(bytes.AsSpan(0, byteCount), format.Encoding, samples);
                builder.AddFrames(samples.AsSpan(0, frames * format.Channels));
                remaining -= frames;
            }
            return builder.Build();
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(bytes);
            ArrayPool<float>.Shared.Return(samples);
        }
    }
}
