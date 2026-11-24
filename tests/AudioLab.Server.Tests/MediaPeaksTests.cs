using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AudioLab.Audio.Tests.Support;
using AudioLab.Audio.Wav;
using AudioLab.Core.Model;
using AudioLab.Server.Media;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using static AudioLab.Server.Tests.MediaTestSupport;

namespace AudioLab.Server.Tests;

public sealed class MediaPeaksTests
{
    private const int Frames = 20_000;

    private static readonly CancellationToken _none = CancellationToken.None;

    // How long a request waits for a rebuild. It is counted on the test's clock, which moves only when a test advances it, so a request
    // is answered 503 when the test says the wait is over and never because the machine was slow.
    private static readonly TimeSpan _wait = TimeSpan.FromSeconds(3);

    // Returns when that many waits have started on the clock, which is when every request has reached its rebuild.
    private static async Task WaitForTimersAsync(ManualTimeProvider time, int count)
    {
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(30));
        while (time.PendingTimers < count)
        {
            await Task.Delay(1, timeout.Token);
        }
    }

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
        return PeakFile.Read(await ReadAllAsync(await store.MediaFiles.OpenPeaksAsync(user, record.Id, _none)));
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
        await Assert.ThrowsAsync<MediaNotFoundException>(() => store.MediaFiles.OpenPeaksAsync(Users.Alice, record.Id, _none));
    }

    [Fact]
    public async Task AnotherUsersPeaksAreNotFound()
    {
        using TempFolder folder = new();
        await using TestStore store = TestStore.On(folder.Path);
        MediaRecord record = await store.UploadAsync(Users.Alice, Wav());
        await Assert.ThrowsAsync<MediaNotFoundException>(() => store.MediaFiles.OpenPeaksAsync(Users.Bob, record.Id, _none));
    }

    private static async Task<byte[]> PeaksOfAsync(TestStore store, UserId user, MediaRecord record)
    {
        return await ReadAllAsync(await store.MediaFiles.OpenPeaksAsync(user, record.Id, _none));
    }

    [Fact]
    public async Task AFileWhosePeaksAreMissingCorruptCutOrOfAnotherFileKeepsItsRecordAndGetsItsPeaksRebuilt()
    {
        using TempFolder folder = new();
        MediaRecord[] damaged = new MediaRecord[4];
        byte[][] original = new byte[4][];
        MediaRecord other;
        await using (TestStore first = TestStore.On(folder.Path))
        {
            for (int index = 0; index < 4; index++)
            {
                damaged[index] = await first.UploadAsync(Users.Alice, Wav(), $"d{index}.wav");
                original[index] = await File.ReadAllBytesAsync(first.Paths.MediaPeaksFile(Users.Alice, damaged[index].Id));
            }
            other = await first.UploadAsync(Users.Alice, Wav(48000));
            File.Delete(first.Paths.MediaPeaksFile(Users.Alice, damaged[0].Id));
            string corrupt = first.Paths.MediaPeaksFile(Users.Alice, damaged[1].Id);
            byte[] bytes = await File.ReadAllBytesAsync(corrupt);
            bytes[0] = (byte)'X';
            await File.WriteAllBytesAsync(corrupt, bytes);
            await using (FileStream stream = new(first.Paths.MediaPeaksFile(Users.Alice, damaged[2].Id), FileMode.Open, FileAccess.Write))
            {
                stream.SetLength(stream.Length - 2);
            }
            File.Copy(first.Paths.MediaPeaksFile(Users.Alice, other.Id), first.Paths.MediaPeaksFile(Users.Alice, damaged[3].Id), overwrite: true);
        }

        await using TestStore store = TestStore.On(folder.Path);

        MediaListing listing = store.MediaFiles.List(Users.Alice);
        Assert.Equal(0, listing.Refused);
        Assert.Equal(5, listing.Files.Count);
        foreach (MediaRecord record in damaged)
        {
            Assert.Equal(record, await store.FindAsync(Users.Alice, record.Id));
        }
        for (int index = 0; index < 4; index++)
        {
            Assert.Equal(original[index], await PeaksOfAsync(store, Users.Alice, damaged[index]));
            Assert.Equal(original[index], await File.ReadAllBytesAsync(store.Paths.MediaPeaksFile(Users.Alice, damaged[index].Id)));
        }
        Assert.Equal(4, store.Rebuilder.Rebuilds);
    }

    [Fact]
    public async Task ARequestThatFindsGoodPeaksRebuildsNothing()
    {
        using TempFolder folder = new();
        await using TestStore store = TestStore.On(folder.Path);
        MediaRecord record = await store.UploadAsync(Users.Alice, Wav());
        await PeaksOfAsync(store, Users.Alice, record);
        await PeaksOfAsync(store, Users.Alice, record);
        Assert.Equal(0, store.Rebuilder.Rebuilds);
    }

    [Fact]
    public async Task ManyRequestsDuringARebuildShareOneAndAreAnswered503UntilItIsDone()
    {
        using TempFolder folder = new();
        TaskCompletionSource gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        await using TestStore store = TestStore.On(
            folder.Path,
            media: new MediaOptions { PeaksRebuildWait = _wait },
            beforeRebuild: () => gate.Task);
        MediaRecord record = await store.UploadAsync(Users.Alice, Wav());
        byte[] original = await File.ReadAllBytesAsync(store.Paths.MediaPeaksFile(Users.Alice, record.Id));
        File.Delete(store.Paths.MediaPeaksFile(Users.Alice, record.Id));

        int armed = store.Time.PendingTimers;
        Task<byte[]>[] waiting = [.. Enumerable.Range(0, 6).Select(_ => Task.Run(() => PeaksOfAsync(store, Users.Alice, record)))];
        await WaitForTimersAsync(store.Time, armed + 6);
        store.Time.Advance(_wait);
        foreach (Task<byte[]> request in waiting)
        {
            MediaPeaksPendingException pending = await Assert.ThrowsAsync<MediaPeaksPendingException>(() => request);
            Assert.True(pending.RetryAfterSeconds >= 1);
        }
        Assert.Equal(0, store.Rebuilder.Rebuilds);

        gate.SetResult();
        byte[][] served = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => Task.Run(() => PeaksOfAsync(store, Users.Alice, record))));

        Assert.All(served, bytes => Assert.Equal(original, bytes));
        Assert.Equal(1, store.Rebuilder.Rebuilds);
    }

    [Fact]
    public async Task RequestsThatArriveWhileARebuildFinishesWithinTheWaitAreServedNotRefused()
    {
        using TempFolder folder = new();
        TaskCompletionSource gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        await using TestStore store = TestStore.On(
            folder.Path,
            media: new MediaOptions { PeaksRebuildWait = _wait },
            beforeRebuild: () => gate.Task);
        MediaRecord record = await store.UploadAsync(Users.Alice, Wav());
        byte[] original = await File.ReadAllBytesAsync(store.Paths.MediaPeaksFile(Users.Alice, record.Id));
        File.Delete(store.Paths.MediaPeaksFile(Users.Alice, record.Id));

        int armed = store.Time.PendingTimers;
        Task<byte[]>[] requests = [.. Enumerable.Range(0, 6).Select(_ => Task.Run(() => PeaksOfAsync(store, Users.Alice, record)))];
        await WaitForTimersAsync(store.Time, armed + 6);
        gate.SetResult();

        Assert.All(await Task.WhenAll(requests), bytes => Assert.Equal(original, bytes));
        Assert.Equal(1, store.Rebuilder.Rebuilds);
    }

    [Fact]
    public async Task AFileWhoseBytesFailTheWavChecksIsRefusedWhenItsPeaksAreAskedFor()
    {
        using TempFolder folder = new();
        await using TestStore store = TestStore.On(folder.Path);
        MediaRecord record = await store.UploadAsync(Users.Alice, Wav());
        MediaRecord keeper = await store.UploadAsync(Users.Alice, Wav());
        string data = store.Paths.MediaDataFile(Users.Alice, record.Id);
        byte[] bytes = await File.ReadAllBytesAsync(data);
        bytes[8] = (byte)'X';
        await File.WriteAllBytesAsync(data, bytes);
        File.Delete(store.Paths.MediaPeaksFile(Users.Alice, record.Id));

        await Assert.ThrowsAsync<MediaNotFoundException>(() => PeaksOfAsync(store, Users.Alice, record));

        Assert.Null(await store.FindAsync(Users.Alice, record.Id));
        Assert.Equal(1, store.MediaFiles.List(Users.Alice).Refused);
        Assert.Equal(keeper, await store.FindAsync(Users.Alice, keeper.Id));
        Assert.True(File.Exists(store.Paths.MediaRecordFile(Users.Alice, record.Id) + ".refused"));
        Assert.False(File.Exists(store.Paths.MediaRecordFile(Users.Alice, record.Id)));

        store.Crash();
        await using TestStore restarted = TestStore.On(folder.Path);
        Assert.Null(await restarted.FindAsync(Users.Alice, record.Id));
        Assert.Equal(1, restarted.MediaFiles.List(Users.Alice).Refused);
        Assert.True(File.Exists(restarted.Paths.MediaDataFile(Users.Alice, record.Id)));
    }

    [Fact]
    public async Task AFileDeletedWhileItsPeaksAreBeingRebuiltLeavesNoPeakFile()
    {
        using TempFolder folder = new();
        TaskCompletionSource gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        MediaOptions options = new() { PeaksRebuildWait = _wait };
        await using TestStore store = TestStore.On(folder.Path, media: options, beforeRebuild: () => gate.Task);
        MediaRecord record = await store.UploadAsync(Users.Alice, Wav());
        File.Delete(store.Paths.MediaPeaksFile(Users.Alice, record.Id));
        int armed = store.Time.PendingTimers;
        Task<byte[]> request = Task.Run(() => PeaksOfAsync(store, Users.Alice, record));
        await WaitForTimersAsync(store.Time, armed + 1);

        await store.MediaFiles.DeleteAsync(Users.Alice, record.Id, _none);
        gate.SetResult();

        await Assert.ThrowsAsync<MediaNotFoundException>(() => request);
        Assert.False(File.Exists(store.Paths.MediaPeaksFile(Users.Alice, record.Id)));
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

    [Fact]
    public async Task ThePeakRouteIsAnswered503WithRetryAfterOnlyWhileARebuildRunsAndThen200()
    {
        TaskCompletionSource gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        ManualTimeProvider time = new();
        await using RunningServer server = await RunningServer.StartAsync(
            new Dictionary<string, string?> { ["AudioLab:Media:PeaksRebuildWait"] = "0:0:3" },
            services =>
            {
                services.AddSingleton<TimeProvider>(time);
                services.AddSingleton(new PeakRebuilder(NullLogger<PeakRebuilder>.Instance, () => gate.Task));
            });
        using HttpResponseMessage created = await server.Client.PostAsync("/api/media?name=a.wav", WavContent(Wav()));
        MediaSummary summary = (await created.Content.ReadFromJsonAsync(MediaJsonContext.Default.MediaSummary))!;
        byte[] original = await server.Client.GetByteArrayAsync($"/api/media/{summary.Id}/peaks");
        string file = Directory.GetFiles(server.StoreFolder, "m*.peaks", SearchOption.AllDirectories).Single();
        File.Delete(file);

        int armed = time.PendingTimers;
        Task<HttpResponseMessage> request = server.Client.GetAsync($"/api/media/{summary.Id}/peaks");
        await WaitForTimersAsync(time, armed + 1);
        time.Advance(TimeSpan.FromSeconds(3));
        using HttpResponseMessage waiting = await request;
        Assert.Equal(HttpStatusCode.ServiceUnavailable, waiting.StatusCode);
        Assert.Equal("3", waiting.Headers.GetValues("Retry-After").Single());

        // The rebuild is held open for as long as the test says, and the clock does not move, so the request cannot give up on it.
        gate.SetResult();
        Assert.Equal(original, await server.Client.GetByteArrayAsync($"/api/media/{summary.Id}/peaks"));
        Assert.True(File.Exists(file));
    }

    [Fact]
    public async Task APeakFileDamagedOnDiskIsServedRebuiltOverHttpAndNeverAsAnError()
    {
        await using RunningServer server = await RunningServer.StartAsync();
        using HttpResponseMessage created = await server.Client.PostAsync("/api/media?name=a.wav", WavContent(Wav()));
        MediaSummary summary = (await created.Content.ReadFromJsonAsync(MediaJsonContext.Default.MediaSummary))!;
        byte[] original = await server.Client.GetByteArrayAsync($"/api/media/{summary.Id}/peaks");
        string file = Directory.GetFiles(server.StoreFolder, "m*.peaks", SearchOption.AllDirectories).Single();

        foreach (Action damage in new Action[]
        {
            () => File.WriteAllBytes(file, [1, 2, 3]),
            () => File.WriteAllBytes(file, original[..(original.Length - 7)]),
            () => File.WriteAllBytes(file, [.. original, 0]),
            () => File.WriteAllBytes(file, []),
        })
        {
            damage();
            using HttpResponseMessage response = await server.Client.GetAsync($"/api/media/{summary.Id}/peaks");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(original, await response.Content.ReadAsByteArrayAsync());
        }
        Assert.Equal(HttpStatusCode.OK, (await server.Client.GetAsync($"/api/media/{summary.Id}")).StatusCode);
    }

    [Fact]
    public async Task PeaksThatARecordLacksAreRebuiltInTheBackgroundWhenTheServerStarts()
    {
        using TempFolder folder = new();
        string peaks;
        await using (TestStore seed = TestStore.On(folder.Path))
        {
            MediaRecord record = await seed.UploadAsync(Users.Alice, Wav());
            peaks = seed.Paths.MediaPeaksFile(Users.Alice, record.Id);
            File.Delete(peaks);
        }

        Dictionary<string, string?> settings = new() { ["AudioLab:Storage:Root"] = folder.Path };
        await using RunningServer server = await RunningServer.StartAsync(settings);
        for (int attempt = 0; attempt < 100 && !File.Exists(peaks); attempt++)
        {
            await Task.Delay(100);
        }

        Assert.True(File.Exists(peaks));
        Assert.NotNull(PeakFile.Read(await File.ReadAllBytesAsync(peaks)));
    }

    private static ByteArrayContent WavContent(byte[] file)
    {
        ByteArrayContent content = new(file);
        content.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
        return content;
    }
}
