using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AudioLab.Core.Model;
using AudioLab.Server.Media;
using static AudioLab.Server.Tests.MediaTestSupport;

namespace AudioLab.Server.Tests;

public sealed class MediaPeaksTests
{
    private const int Frames = 20_000;

    private static readonly CancellationToken _none = CancellationToken.None;

    // Left is a sawtooth, right holds -3000 with one spike and one dip, so every level has something to find.
    private static short[] KnownSignal()
    {
        short[] samples = new short[Frames * 2];
        for (int frame = 0; frame < Frames; frame++)
        {
            samples[frame * 2] = (short)((frame % 1000) - 500);
            samples[(frame * 2) + 1] = frame switch { 12_345 => 32767, 777 => -32768, _ => -3000 };
        }
        return samples;
    }

    // The expected peaks, worked out from the shorts with no code of the builder's.
    private static short[] Expected(short[] samples, int framesPerPeak)
    {
        int peaks = (Frames + framesPerPeak - 1) / framesPerPeak;
        short[] result = new short[peaks * 4];
        for (int peak = 0; peak < peaks; peak++)
        {
            for (int channel = 0; channel < 2; channel++)
            {
                int length = Math.Min(framesPerPeak, Frames - (peak * framesPerPeak));
                IEnumerable<short> run = Enumerable.Range(peak * framesPerPeak, length).Select(frame => samples[(frame * 2) + channel]);
                result[(peak * 4) + (channel * 2)] = run.Min();
                result[(peak * 4) + (channel * 2) + 1] = run.Max();
            }
        }
        return result;
    }

    private static async Task<PeakData> StoredPeaksAsync(TestStore store, UserId user, MediaRecord record)
    {
        return PeakFile.Read(await ReadAllAsync(store.MediaFiles.OpenPeaks(user, record.Id)));
    }

    [Theory]
    [InlineData(SampleEncoding.Pcm16)]
    [InlineData(SampleEncoding.Pcm24)]
    [InlineData(SampleEncoding.Pcm32)]
    [InlineData(SampleEncoding.Float32)]
    public async Task UploadedPeaksAreTheExactMinimumAndMaximumOfAKnownSignalInEveryEncoding(SampleEncoding encoding)
    {
        using TempFolder folder = new();
        await using TestStore store = TestStore.On(folder.Path);
        short[] samples = KnownSignal();

        MediaRecord record = await store.UploadAsync(Users.Alice, TestWav.FromShorts(encoding, 2, 44100, samples));
        PeakData peaks = await StoredPeaksAsync(store, Users.Alice, record);

        Assert.Equal((2, 44100, (long)Frames), (peaks.Channels, peaks.SampleRate, peaks.Frames));
        Assert.Equal([64, 256, 1024, 4096, 16384], peaks.Levels.Select(level => level.FramesPerPeak));
        foreach (PeakLevel level in peaks.Levels)
        {
            bool same = Expected(samples, level.FramesPerPeak).AsSpan().SequenceEqual(level.Values);
            Assert.True(same, $"{encoding} level {level.FramesPerPeak}");
        }
        Assert.Equal(32767, peaks.Levels[4].Values[3]);
        Assert.Equal(-32768, peaks.Levels[4].Values[2]);
    }

    [Fact]
    public async Task AFloatFileBeyondFullScaleIsClampedToTheLargestPeakValues()
    {
        using TempFolder folder = new();
        await using TestStore store = TestStore.On(folder.Path);
        byte[] file = TestWav.Build(SampleEncoding.Float32, 1, 48000, 500, (frame, _) => frame == 10 ? 2.5 : frame == 20 ? -9.0 : 0.25);

        MediaRecord record = await store.UploadAsync(Users.Alice, file);
        PeakData peaks = await StoredPeaksAsync(store, Users.Alice, record);

        Assert.Equal([-32768, 32767], peaks.Levels[4].Values.ToArray());
    }

    [Fact]
    public async Task APeakFileIsStoredBesideEveryFileAndRemovedWithIt()
    {
        using TempFolder folder = new();
        await using TestStore store = TestStore.On(folder.Path);
        MediaRecord record = await store.UploadAsync(Users.Alice, Wav());
        Assert.True(File.Exists(store.Paths.MediaPeaksFile(Users.Alice, record.Id)));

        await store.MediaFiles.DeleteAsync(Users.Alice, record.Id, _none);

        Assert.False(File.Exists(store.Paths.MediaPeaksFile(Users.Alice, record.Id)));
        Assert.Throws<MediaNotFoundException>(() => store.MediaFiles.OpenPeaks(Users.Alice, record.Id));
    }

    [Fact]
    public async Task AnotherUsersPeaksAreNotFound()
    {
        using TempFolder folder = new();
        await using TestStore store = TestStore.On(folder.Path);
        MediaRecord record = await store.UploadAsync(Users.Alice, Wav());
        Assert.Throws<MediaNotFoundException>(() => store.MediaFiles.OpenPeaks(Users.Bob, record.Id));
    }

    [Fact]
    public async Task AFileWhosePeaksAreMissingTruncatedOrOfAnotherFileIsRefusedAtStart()
    {
        using TempFolder folder = new();
        MediaRecord good;
        MediaRecord missing;
        MediaRecord truncated;
        MediaRecord swapped;
        MediaRecord longer;
        await using (TestStore first = TestStore.On(folder.Path))
        {
            good = await first.UploadAsync(Users.Alice, Wav());
            missing = await first.UploadAsync(Users.Alice, Wav());
            truncated = await first.UploadAsync(Users.Alice, Wav());
            swapped = await first.UploadAsync(Users.Alice, Wav());
            longer = await first.UploadAsync(Users.Alice, Wav(48000));
            File.Delete(first.Paths.MediaPeaksFile(Users.Alice, missing.Id));
            string cut = first.Paths.MediaPeaksFile(Users.Alice, truncated.Id);
            await using (FileStream stream = new(cut, FileMode.Open, FileAccess.Write))
            {
                stream.SetLength(stream.Length - 2);
            }
            string theirs = first.Paths.MediaPeaksFile(Users.Alice, longer.Id);
            File.Copy(theirs, first.Paths.MediaPeaksFile(Users.Alice, swapped.Id), overwrite: true);
        }

        await using TestStore store = TestStore.On(folder.Path);

        Assert.Equal(good, store.Media.Find(Users.Alice, good.Id));
        Assert.Equal(longer, store.Media.Find(Users.Alice, longer.Id));
        Assert.Null(store.Media.Find(Users.Alice, missing.Id));
        Assert.Null(store.Media.Find(Users.Alice, truncated.Id));
        Assert.Null(store.Media.Find(Users.Alice, swapped.Id));
        Assert.Equal(3, store.MediaFiles.List(Users.Alice).Refused);
    }

    [Fact]
    public async Task PeaksWithNoRecordAreSweptAtStart()
    {
        using TempFolder folder = new();
        string orphan;
        string temp;
        await using (TestStore first = TestStore.On(folder.Path))
        {
            MediaId stray = MediaId.From("med_stray");
            await first.UploadAsync(Users.Alice, Wav());
            orphan = first.Paths.MediaPeaksFile(Users.Alice, stray);
            temp = first.Paths.MediaPeaksTempFile(Users.Alice, stray);
            await File.WriteAllBytesAsync(orphan, [1, 2, 3]);
            await File.WriteAllBytesAsync(temp, [1, 2, 3]);
        }

        await using TestStore store = TestStore.On(folder.Path);

        Assert.False(File.Exists(orphan));
        Assert.False(File.Exists(temp));
        Assert.Single(store.MediaFiles.List(Users.Alice).Files);
    }

    [Fact]
    public void ASampleDecoderGivesTheValueEachEncodingStores()
    {
        float[] decoded = new float[2];
        WavSamples.ToFloat([0x00, 0x80, 0xFF, 0x7F], SampleEncoding.Pcm16, decoded);
        Assert.Equal([-1f, 32767 / 32768f], decoded);
        WavSamples.ToFloat([0x00, 0x00, 0x80, 0xFF, 0xFF, 0x7F], SampleEncoding.Pcm24, decoded);
        Assert.Equal([-1f, 8388607 / 8388608f], decoded);
        WavSamples.ToFloat([0x00, 0x00, 0x00, 0x80, 0x00, 0x00, 0x00, 0x40], SampleEncoding.Pcm32, decoded);
        Assert.Equal([-1f, 0.5f], decoded);
        WavSamples.ToFloat([0x00, 0x00, 0x00, 0x3F, 0x00, 0x00, 0x80, 0xBF], SampleEncoding.Float32, decoded);
        Assert.Equal([0.5f, -1f], decoded);
        Assert.Throws<ArgumentException>(() => WavSamples.ToFloat([1, 2, 3], SampleEncoding.Pcm16, decoded));
        Assert.Throws<ArgumentException>(() => WavSamples.ToFloat(new byte[8], SampleEncoding.Pcm16, new float[1]));
    }

    [Fact]
    public async Task ThePeakRouteServesTheStoredFileWithRangesAndTheRightHeaders()
    {
        await using RunningServer server = await RunningServer.StartAsync();
        short[] samples = KnownSignal();
        using HttpResponseMessage created = await server.Client.PostAsync(
            "/api/media?name=known.wav",
            WavContent(TestWav.FromShorts(SampleEncoding.Pcm16, 2, 44100, samples)));
        MediaSummary summary = (await created.Content.ReadFromJsonAsync(MediaJsonContext.Default.MediaSummary))!;

        using HttpResponseMessage response = await server.Client.GetAsync($"/api/media/{summary.Id}/peaks");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(PeakFile.MediaType, response.Content.Headers.ContentType?.MediaType);
        Assert.Equal($"\"{summary.Sha256}-peaks1\"", response.Headers.ETag?.Tag);
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Contains("immutable", response.Headers.CacheControl?.ToString(), StringComparison.Ordinal);
        byte[] bytes = await response.Content.ReadAsByteArrayAsync();
        PeakData peaks = PeakFile.Read(bytes);
        Assert.True(Expected(samples, 1024).AsSpan().SequenceEqual(peaks.Levels[2].Values));

        PeakHeader header = PeakFile.ReadHeader(bytes);
        using HttpRequestMessage ranged = new(HttpMethod.Get, $"/api/media/{summary.Id}/peaks");
        ranged.Headers.Range = new RangeHeaderValue(0, header.TableEnd - 1);
        using HttpResponseMessage head = await server.Client.SendAsync(ranged);
        Assert.Equal(HttpStatusCode.PartialContent, head.StatusCode);
        Assert.Equal(bytes[..header.TableEnd], await head.Content.ReadAsByteArrayAsync());

        Assert.Equal(HttpStatusCode.BadRequest, (await server.Client.GetAsync("/api/media/..%2Fx/peaks")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await server.Client.GetAsync("/api/media/med_nothing/peaks")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await server.Client.DeleteAsync($"/api/media/{summary.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await server.Client.GetAsync($"/api/media/{summary.Id}/peaks")).StatusCode);
    }

    [Fact]
    public async Task AnotherUsersPeaksAreAnsweredLikeAFileThatDoesNotExist()
    {
        await using RunningServer server = await RunningServer.StartAsync(new Dictionary<string, string?>
        {
            ["AudioLab:Door:Kind"] = "Swarm",
            ["AudioLab:Door:ProxySecret"] = "0123456789abcdef0123456789abcdef",
        });
        using HttpRequestMessage upload = new(HttpMethod.Post, "/api/media?name=a.wav") { Content = WavContent(Wav(100)) };
        upload.Headers.Add("X-AudioLab-Secret", "0123456789abcdef0123456789abcdef");
        upload.Headers.Add("X-AudioLab-User", "alice");
        using HttpResponseMessage created = await server.Client.SendAsync(upload);
        MediaSummary summary = (await created.Content.ReadFromJsonAsync(MediaJsonContext.Default.MediaSummary))!;

        foreach ((string user, string id) in new[] { ("bob", summary.Id), ("bob", "med_nothing") })
        {
            using HttpRequestMessage request = new(HttpMethod.Get, $"/api/media/{id}/peaks");
            request.Headers.Add("X-AudioLab-Secret", "0123456789abcdef0123456789abcdef");
            request.Headers.Add("X-AudioLab-User", user);
            using HttpResponseMessage response = await server.Client.SendAsync(request);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }

    private static ByteArrayContent WavContent(byte[] file)
    {
        ByteArrayContent content = new(file);
        content.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
        return content;
    }
}
