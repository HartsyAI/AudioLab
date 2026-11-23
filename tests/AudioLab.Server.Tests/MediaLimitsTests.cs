using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AudioLab.Audio.Tests.Support;
using AudioLab.Core.Model;
using AudioLab.Server.Media;
using Microsoft.Extensions.DependencyInjection;

namespace AudioLab.Server.Tests;

public sealed class MediaLimitsTests
{
    private const string Secret = "0123456789abcdef0123456789abcdef";

    private static Dictionary<string, string?> Swarm() => new() { ["AudioLab:Door:Kind"] = "Swarm", ["AudioLab:Door:ProxySecret"] = Secret };

    private static HttpRequestMessage Request(HttpMethod method, string path, string? user = null, HttpContent? content = null)
    {
        HttpRequestMessage request = new(method, path) { Content = content };
        if (user is not null)
        {
            request.Headers.Add("X-AudioLab-Secret", Secret);
            request.Headers.Add("X-AudioLab-User", user);
        }
        return request;
    }

    private static async Task<MediaLimits> LimitsAsync(RunningServer server, string? user = null)
    {
        using HttpRequestMessage request = Request(HttpMethod.Get, "/api/media/limits", user);
        using HttpResponseMessage response = await server.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync(MediaJsonContext.Default.MediaLimits))!;
    }

    private static async Task<long> UploadAsync(RunningServer server, string? user = null, int frames = 24000)
    {
        byte[] file = TestWav.Build(SampleEncoding.Pcm16, 2, 48000, frames);
        ByteArrayContent content = new(file);
        content.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
        using HttpRequestMessage request = Request(HttpMethod.Post, "/api/media?name=take.wav", user, content);
        using HttpResponseMessage response = await server.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return file.Length;
    }

    private static async Task WaitForAsync(Func<Task<bool>> condition)
    {
        DateTime until = DateTime.UtcNow.AddSeconds(20);
        while (!await condition())
        {
            Assert.True(DateTime.UtcNow < until, "the condition did not come true in 20 seconds");
            await Task.Delay(20);
        }
    }

    [Fact]
    public async Task ANewUserIsToldTheConfiguredLimitsAndThatNothingIsUsed()
    {
        MediaOptions defaults = new();
        await using RunningServer server = await RunningServer.StartAsync();

        MediaLimits limits = await LimitsAsync(server);

        Assert.Equal(
            new MediaLimits(
                defaults.MaxUploadBytes,
                defaults.MaxFilesPerUser,
                defaults.MaxBytesPerUser,
                defaults.MaxConcurrentUploadsPerUser,
                0,
                0,
                defaults.MaxFilesPerUser,
                defaults.MaxBytesPerUser,
                0),
            limits);
    }

    [Fact]
    public async Task WhatAUserHasStoredIsCountedAndTakenFromWhatRemains()
    {
        await using RunningServer server = await RunningServer.StartAsync();
        long first = await UploadAsync(server);
        long second = await UploadAsync(server, frames: 48000);

        MediaLimits limits = await LimitsAsync(server);

        Assert.Equal((2, first + second), (limits.Files, limits.Bytes));
        Assert.Equal((limits.MaxFiles - 2, limits.MaxBytes - first - second), (limits.FilesRemaining, limits.BytesRemaining));
        Assert.Equal(0, limits.UploadsRunning);
    }

    [Fact]
    public async Task EachUserSeesTheirOwnUseAndNeverAnotherUsersNameOrNumbers()
    {
        await using RunningServer server = await RunningServer.StartAsync(Swarm());
        long alice = await UploadAsync(server, "alice");

        MediaLimits aliceLimits = await LimitsAsync(server, "alice");
        MediaLimits bobLimits = await LimitsAsync(server, "bob");
        using HttpRequestMessage request = Request(HttpMethod.Get, "/api/media/limits", "alice");
        string json = await (await server.Client.SendAsync(request)).Content.ReadAsStringAsync();

        Assert.Equal((1, alice), (aliceLimits.Files, aliceLimits.Bytes));
        Assert.Equal((0, 0L), (bobLimits.Files, bobLimits.Bytes));
        Assert.DoesNotContain("alice", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("owner", json, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(
            [
                "bytes", "bytesRemaining", "files", "filesRemaining", "maxBytes",
                "maxConcurrentUploads", "maxFiles", "maxUploadBytes", "uploadsRunning",
            ],
            JsonDocument.Parse(json).RootElement.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task TheRemainingNumbersAreWhatIsLeftOfTheConfiguredLimits()
    {
        Dictionary<string, string?> settings = new()
        {
            ["AudioLab:Media:MaxFilesPerUser"] = "1",
            ["AudioLab:Media:MaxBytesPerUser"] = "1000000",
            ["AudioLab:Media:MaxUploadBytes"] = "1000000",
        };
        await using RunningServer server = await RunningServer.StartAsync(settings);
        long size = await UploadAsync(server, frames: 24000);

        MediaLimits full = await LimitsAsync(server);

        Assert.Equal((1, 0, 1_000_000 - size), (full.Files, full.FilesRemaining, full.BytesRemaining));
        Assert.True(size < 1_000_000);
    }

    [Fact]
    public async Task TheLimitsComeFromTheQuotaSoAReplacementQuotaIsTheOneTheyReport()
    {
        await using RunningServer server = await RunningServer.StartAsync(
            null,
            services => services.AddSingleton<IMediaQuota>(new FixedQuota(7, 5000)));
        await UploadAsync(server);

        MediaLimits limits = await LimitsAsync(server);

        Assert.Equal((7, 5000L), (limits.MaxFiles, limits.MaxBytes));
        Assert.Equal((6, 0L), (limits.FilesRemaining, limits.BytesRemaining));
    }

    [Fact]
    public async Task AQuotaThatOnlyRefusesStillAnswersWithNoFixedLimit()
    {
        await using RunningServer server = await RunningServer.StartAsync(
            null,
            services => services.AddSingleton<IMediaQuota>(new RefusingQuota()));
        await UploadAsync(server);

        MediaLimits limits = await LimitsAsync(server);

        Assert.Equal((int.MaxValue, long.MaxValue), (limits.MaxFiles, limits.MaxBytes));
        Assert.Equal((int.MaxValue - 1, 1), (limits.FilesRemaining, limits.Files));
    }

    // A quota written before the limits existed: it implements only the refusal.
    private sealed class RefusingQuota : IMediaQuota
    {
        public ValueTask<string?> RefusalAsync(UserId user, MediaUsage usage, long incomingBytes, CancellationToken cancellationToken)
        {
            return ValueTask.FromResult<string?>(null);
        }
    }

    private sealed class FixedQuota(int files, long bytes) : IMediaQuota
    {
        public ValueTask<string?> RefusalAsync(UserId user, MediaUsage usage, long incomingBytes, CancellationToken cancellationToken)
        {
            return ValueTask.FromResult<string?>(null);
        }

        public ValueTask<MediaQuotaLimits> LimitsAsync(UserId user, CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(new MediaQuotaLimits(files, bytes));
        }
    }

    [Fact]
    public async Task ARunningUploadIsCountedWithTheBytesItHasReceivedAndIsGoneWhenItFinishes()
    {
        await using RunningServer server = await RunningServer.StartAsync();
        byte[] file = TestWav.Build(SampleEncoding.Pcm16, 2, 48000, 24000);
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Stalling slow = new(file, release.Task);
        slow.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
        using HttpRequestMessage upload = Request(HttpMethod.Post, "/api/media?name=slow.wav", null, slow);
        Task<HttpResponseMessage> running = server.Client.SendAsync(upload);

        await WaitForAsync(async () => (await LimitsAsync(server)).UploadsRunning == 1);
        MediaLimits during = await LimitsAsync(server);
        release.SetResult();
        using HttpResponseMessage finished = await running;
        MediaLimits after = await LimitsAsync(server);

        Assert.Equal(HttpStatusCode.Created, finished.StatusCode);
        Assert.Equal((1, 1), (during.Files, during.UploadsRunning));
        Assert.Equal(during.MaxFiles - 1, during.FilesRemaining);
        Assert.Equal((1, file.Length, 0), (after.Files, after.Bytes, after.UploadsRunning));
    }

    // A body that sends its first bytes and then waits, so that an upload is running while the limits are asked for.
    private sealed class Stalling(byte[] file, Task release) : HttpContent
    {
        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            await stream.WriteAsync(file.AsMemory(0, 1000));
            await stream.FlushAsync();
            await release;
            await stream.WriteAsync(file.AsMemory(1000));
        }

        protected override bool TryComputeLength(out long length)
        {
            length = file.Length;
            return true;
        }
    }

    [Fact]
    public async Task TheRouteIsBehindTheDoorAndALiteralPathBesideTheIdRoute()
    {
        await using RunningServer server = await RunningServer.StartAsync(Swarm());

        using HttpRequestMessage anonymous = Request(HttpMethod.Get, "/api/media/limits");
        using HttpResponseMessage refused = await server.Client.SendAsync(anonymous);
        using HttpRequestMessage signedIn = Request(HttpMethod.Get, "/api/media/limits", "alice");
        using HttpResponseMessage allowed = await server.Client.SendAsync(signedIn);
        using HttpRequestMessage byId = Request(HttpMethod.Get, "/api/media/med_that_is_not_there", "alice");
        using HttpResponseMessage missing = await server.Client.SendAsync(byId);

        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }
}
