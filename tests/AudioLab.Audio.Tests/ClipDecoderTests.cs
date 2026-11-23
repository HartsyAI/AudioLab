using AudioLab.Audio.Live;
using AudioLab.Audio.Wav;

namespace AudioLab.Audio.Tests;

public sealed class ClipDecoderTests
{
    private const int Rate = 8000;

    private static byte[] Wav(RenderedAudio audio, WavFormat format)
    {
        using MemoryStream stream = new();
        WavExporter.Write(audio, stream, new WavOptions { Format = format, Dither = DitherKind.None });
        return stream.ToArray();
    }

    private static Task Pause(CancellationToken cancellationToken) => Task.CompletedTask;

    [Theory]
    [InlineData(WavFormat.Pcm16, 1e-4)]
    [InlineData(WavFormat.Pcm24, 1e-6)]
    [InlineData(WavFormat.Float32, 0)]
    public async Task EveryFormatTheStoreAcceptsDecodesToTheSamplesThatWereWritten(WavFormat format, double tolerance)
    {
        float[] left = [.. Enumerable.Range(0, 40000).Select(i => (float)(0.6 * Math.Sin(i * 0.05)))];
        float[] right = [.. left.Select(sample => -sample * 0.5f)];
        byte[] file = Wav(new RenderedAudio(new AudioFormat(Rate, 2), left, right), format);

        PcmAudio decoded = await ClipDecoder.DecodeAsync(file, Pause, CancellationToken.None);

        Assert.Equal(Rate, decoded.SampleRate);
        Assert.Equal(2, decoded.ChannelCount);
        Assert.Equal(left.Length, decoded.Frames);
        for (int i = 0; i < left.Length; i += 7)
        {
            Assert.Equal(left[i], decoded.Channel(0)[i], tolerance);
            Assert.Equal(right[i], decoded.Channel(1)[i], tolerance);
        }
    }

    [Fact]
    public async Task TheDecoderPausesBetweenSlicesSoAWorkerCanAnswerItsAudioMeanwhile()
    {
        byte[] file = Wav(new RenderedAudio(new AudioFormat(Rate, 2), new float[100000], new float[100000]), WavFormat.Pcm16);
        int pauses = 0;

        await ClipDecoder.DecodeAsync(file, _ => Task.FromResult(++pauses), CancellationToken.None);

        Assert.True(pauses >= 100000 / 16384);
    }

    [Fact]
    public async Task AFileTheReaderRefusesIsRefusedWithItsReasonAndNotDecoded()
    {
        byte[] notWav = "RIFX....WAVE"u8.ToArray();
        byte[] cut = Wav(new RenderedAudio(new AudioFormat(Rate, 2), new float[1000], new float[1000]), WavFormat.Pcm16)[..500];

        WavFormatException first = await Assert.ThrowsAsync<WavFormatException>(() => ClipDecoder.DecodeAsync(notWav, Pause, CancellationToken.None));
        WavFormatException second = await Assert.ThrowsAsync<WavFormatException>(() => ClipDecoder.DecodeAsync(cut, Pause, CancellationToken.None));

        Assert.Contains("RIFF", first.Message);
        Assert.Contains("truncated", second.Message);
    }
}
