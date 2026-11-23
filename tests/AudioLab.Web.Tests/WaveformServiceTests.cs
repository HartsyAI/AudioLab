using System.Net;
using AudioLab.Core.Model;
using AudioLab.Core.Rules;
using AudioLab.Web.Services;
using AudioLab.Web.Waveforms;

namespace AudioLab.Web.Tests;

/// <summary>Holds the tests of the waveform a clip is given: its window, its path, and its states.</summary>
public sealed class WaveformServiceTests
{
    private static readonly Uri _site = new("http://audiolab.test/");

    private static AudioClip Clip(string media, double offset, double trimEnd, double duration = 4)
    {
        return Song.Audio("a1", 0, duration) with { Media = MediaId.From(media), Offset = offset, TrimEnd = trimEnd };
    }

    private static byte[] Peaks(int seconds)
    {
        float[] samples = new float[seconds * 44100];
        for (int frame = 44100 * seconds / 2; frame < samples.Length; frame++)
        {
            samples[frame] = frame % 2 == 0 ? 0.6f : -0.6f;
        }
        PeakBuilder builder = new(1, 44100);
        builder.AddFrames(samples);
        return PeakFile.Write(builder.Build());
    }

    private static WaveformService Service(HttpMessageHandler handler)
    {
        return new WaveformService(new PeakStore(new MediaApi(new HttpClient(handler) { BaseAddress = _site }), TimeProvider.System));
    }

    private static HttpResponseMessage Slice(HttpRequestMessage request, byte[] file)
    {
        System.Net.Http.Headers.RangeItemHeaderValue range = request.Headers.Range!.Ranges.Single();
        int from = (int)range.From!.Value;
        int to = (int)Math.Min(range.To!.Value, file.Length - 1);
        return new HttpResponseMessage(HttpStatusCode.PartialContent) { Content = new ByteArrayContent(file[from..(to + 1)]) };
    }

    private static async Task<ClipWave> ReadyAsync(WaveformService service, AudioClip clip, double width)
    {
        ClipWave wave = service.For(clip, width, 34);
        for (int attempt = 0; attempt < 400 && wave.Phase == WavePhase.Loading; attempt++)
        {
            await Task.Delay(5);
            wave = service.For(clip, width, 34);
        }
        return wave;
    }

    /// <summary>A clip has no path until its peaks arrive, then the same string every time, so an untouched clip is not drawn again.</summary>
    [Fact]
    public async Task AClipHasNoPathUntilItsPeaksArriveThenTheSameStringEveryTime()
    {
        byte[] file = Peaks(4);
        WaveformService service = Service(new RecordingHandler(request => Slice(request, file)));
        AudioClip clip = Clip("m1", 0, 0);

        ClipWave first = service.For(clip, 136, 34);
        ClipWave ready = await ReadyAsync(service, clip, 136);
        ClipWave again = service.For(clip, 136, 34);

        Assert.Equal(WavePhase.Loading, first.Phase);
        Assert.Null(first.Path);
        Assert.Equal(WavePhase.Ready, ready.Phase);
        Assert.Same(ready.Path, again.Path);
        Assert.Equal(ready, again);
        Assert.Equal(1, service.Count);
    }

    /// <summary>The path is of the window the clip plays: a trimmed clip and a whole one draw different paths from the same file.</summary>
    [Fact]
    public async Task ThePathIsOfTheWindowTheClipPlays()
    {
        byte[] file = Peaks(4);
        WaveformService service = Service(new RecordingHandler(request => Slice(request, file)));

        ClipWave whole = await ReadyAsync(service, Clip("m1", 0, 0), 136);
        ClipWave quiet = await ReadyAsync(service, Clip("m1", 0, 2), 68);
        ClipWave loud = await ReadyAsync(service, Clip("m1", 2, 0), 68);

        Assert.NotEqual(whole.Path, quiet.Path);
        Assert.NotEqual(quiet.Path, loud.Path);
        Assert.Equal(3, service.Count);
    }

    /// <summary>A file that cannot be had gives a failed waveform with the reason, and selecting the clip asks again.</summary>
    [Fact]
    public async Task AFileThatCannotBeHadGivesAFailedWaveformAndSelectingTheClipAsksAgain()
    {
        bool fail = true;
        byte[] file = Peaks(4);
        RecordingHandler handler = new(request => fail
            ? new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("{\"title\":\"There is no such media file.\"}", System.Text.Encoding.UTF8, "application/problem+json"),
            }
            : Slice(request, file));
        WaveformService service = Service(handler);
        AudioClip clip = Clip("m1", 0, 0);

        ClipWave failed = await ReadyAsync(service, clip, 136);
        fail = false;
        service.RetryIfFailed(clip);
        ClipWave after = await ReadyAsync(service, clip, 136);

        Assert.Equal(new ClipWave(WavePhase.Failed, null, "There is no such media file."), failed);
        Assert.Equal(WavePhase.Ready, after.Phase);
    }

    /// <summary>Clips of one file share one fetch, however many clips and windows there are.</summary>
    [Fact]
    public async Task ClipsOfOneFileShareOneFetch()
    {
        byte[] file = Peaks(4);
        RecordingHandler handler = new(request => Slice(request, file));
        WaveformService service = Service(handler);

        for (int index = 0; index < 50; index++)
        {
            service.For(Clip("m1", index % 3, 0), 100, 34);
        }
        await ReadyAsync(service, Clip("m1", 0, 0), 100);

        Assert.Equal(2, handler.Requests.Count);
    }
}
