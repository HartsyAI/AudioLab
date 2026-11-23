using System.Net;
using AudioLab.Core.Model;
using AudioLab.Core.Rules;
using AudioLab.Web.Waveforms;
using static AudioLab.Web.Tests.PeakSupport;

namespace AudioLab.Web.Tests;

/// <summary>Holds the tests of the waveform a clip is given: its window, its path, its cache and its states.</summary>
public sealed class WaveformServiceTests
{
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

    /// <summary>A clip has no path until its peaks arrive, then the same string every time, so an untouched clip is not drawn again.</summary>
    [Fact]
    public async Task AClipHasNoPathUntilItsPeaksArriveThenTheSameStringEveryTime()
    {
        byte[] file = Peaks(4);
        WaveformService service = new(Store(new RecordingHandler(request => Slice(request, file))));
        AudioClip clip = Clip("m1", 0, 0);

        ClipWave first = service.For(clip, 136, 34);
        ClipWave ready = await SettledAsync(service, clip, 136);
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
        WaveformService service = new(Store(new RecordingHandler(request => Slice(request, file))));

        ClipWave whole = await SettledAsync(service, Clip("m1", 0, 0), 136);
        ClipWave quiet = await SettledAsync(service, Clip("m1", 0, 2), 68);
        ClipWave loud = await SettledAsync(service, Clip("m1", 2, 0), 68);

        Assert.NotEqual(whole.Path, quiet.Path);
        Assert.NotEqual(quiet.Path, loud.Path);
        Assert.Equal(3, service.Count);
    }

    /// <summary>A file that cannot be had gives a failed waveform with the reason, and asking again after a retry gives the waveform.</summary>
    [Fact]
    public async Task AFileThatCannotBeHadGivesAFailedWaveformAndARetryGivesTheWaveform()
    {
        bool fail = true;
        byte[] file = Peaks(4);
        RecordingHandler handler = new(request => fail ? Refusal(HttpStatusCode.NotFound, "There is no such media file.") : Slice(request, file));
        WaveformService service = new(Store(handler));
        AudioClip clip = Clip("m1", 0, 0);

        ClipWave failed = await SettledAsync(service, clip, 136);
        fail = false;
        service.RetryIfFailed(clip);
        ClipWave after = await SettledAsync(service, clip, 136);

        Assert.Equal(new ClipWave(WavePhase.Failed, null, "There is no such media file."), failed);
        Assert.Equal(WavePhase.Ready, after.Phase);
    }

    /// <summary>Clips of one file share one fetch, however many clips and windows there are.</summary>
    [Fact]
    public async Task ClipsOfOneFileShareOneFetch()
    {
        byte[] file = Peaks(4);
        RecordingHandler handler = new(request => Slice(request, file));
        WaveformService service = new(Store(handler));

        for (int index = 0; index < 50; index++)
        {
            service.For(Clip("m1", index % 3, 0), 100, 34);
        }
        await SettledAsync(service, Clip("m1", 0, 0), 100);

        Assert.Equal(2, handler.Requests.Count);
    }

    /// <summary>A clip that has its path keeps it when its level of peaks is dropped for room, and does not fetch that level again.</summary>
    [Fact]
    public async Task AClipThatHasItsPathDoesNotFetchADroppedLevelAgain()
    {
        byte[] file = Peaks(4);
        RecordingHandler handler = new(request => Slice(request, file));
        PeakStore store = Store(handler, budget: 1);
        WaveformService service = new(store);
        AudioClip first = Clip("m1", 0, 0);
        AudioClip second = Clip("m2", 0, 0);

        ClipWave drawn = await SettledAsync(service, first, 136);
        await SettledAsync(service, second, 136);
        int requests = handler.Requests.Count;
        ClipWave again = service.For(first, 136, 34);

        Assert.Equal(drawn, again);
        Assert.Same(drawn.Path, again.Path);
        Assert.Equal(requests, handler.Requests.Count);
    }

    /// <summary>The paths are held within a budget of characters and the least recently drawn go first, whole, never all at once.</summary>
    [Fact]
    public async Task ThePathsAreHeldWithinABudgetOfCharactersAndTheLeastRecentlyDrawnGoFirst()
    {
        byte[] file = Peaks(8);
        RecordingHandler handler = new(request => Slice(request, file));
        WaveformService service = new(Store(handler), 12_000);
        AudioClip[] clips = [.. Enumerable.Range(0, 6).Select(index => Clip("m1", index, 0, 8) with { TrimEnd = 8 - index - 1 })];

        ClipWave first = await SettledAsync(service, clips[0], 100);
        for (int index = 1; index < clips.Length; index++)
        {
            service.For(clips[index], 100, 34);
            service.For(clips[0], 100, 34);
        }

        Assert.InRange(service.Characters, 1, 12_000);
        Assert.InRange(service.Count, 2, 5);
        Assert.Same(first.Path, service.For(clips[0], 100, 34).Path);
    }
}
