using System.Diagnostics;
using System.Net;
using AudioLab.Audio.Tests.Support;
using AudioLab.Core.Model;
using AudioLab.Server.Decoding;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AudioLab.Server.Tests;

public sealed class DecodedUploadTests
{
    private const string Mp3 = "audio/mpeg";
    private const string Wav = "audio/wav";
    private static readonly byte[] _compressedFile = "not really an mp3, the fake decoder decides"u8.ToArray();

    private static Task<RunningServer> StartInstalledAsync(FakeDecoderLauncher launcher, Dictionary<string, string?>? settings = null)
    {
        byte[] archive = DecoderTestSupport.Archive(DecoderTestSupport.Binary);
        DecoderBuild build = DecoderTestSupport.BuildFor(archive, DecoderTestSupport.Binary);
        return InstalledAsync(build, launcher, settings);
    }

    private static async Task<RunningServer> InstalledAsync(DecoderBuild build, FakeDecoderLauncher launcher, Dictionary<string, string?>? settings)
    {
        RunningServer server = await DecoderTestSupport.StartAsync(build, new FakeDecoderDownloader(), launcher, settings);
        DecoderTestSupport.PlaceBinary(server, build, DecoderTestSupport.Binary);
        return server;
    }

    private static Dictionary<string, string?> TimeLimit(string time) => new() { ["AudioLab:Decoder:Timeout"] = time };

    [Theory]
    [InlineData("audio/mpeg", "mp3")]
    [InlineData("audio/mp3", "mp3")]
    [InlineData("audio/mp4", "mov")]
    [InlineData("audio/x-m4a", "mov")]
    [InlineData("audio/flac", "flac")]
    [InlineData("audio/ogg", "ogg")]
    [InlineData("AUDIO/MPEG", "mp3")]
    [InlineData("text/plain", null)]
    public void EachCompressedTypeIsReadWithItsOwnDemuxerOnly(string mediaType, string? demuxer)
    {
        Assert.Equal(demuxer, DecodedUploadService.DemuxerFor(mediaType));
    }

    [Fact]
    public async Task ADecodedFileIsStoredAsWav()
    {
        FakeDecoderLauncher launcher = new() { Behaviour = FakeDecoderLauncher.Mode.Succeed };
        await using RunningServer server = await StartInstalledAsync(launcher);

        using HttpResponseMessage response = await DecoderTestSupport.UploadAsync(server, _compressedFile, "song.mp3", Mp3);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(1, launcher.Starts);
        Assert.Empty(DecoderTestSupport.TempFiles(server));
    }

    [Fact]
    public async Task AnUploadIsReadWithOnlyTheDemuxerItsContentTypeNames()
    {
        FakeDecoderLauncher launcher = new() { Behaviour = FakeDecoderLauncher.Mode.Succeed };
        await using RunningServer server = await StartInstalledAsync(launcher);

        using HttpResponseMessage response = await DecoderTestSupport.UploadAsync(server, _compressedFile, "song.mp3", Mp3);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        int at = launcher.LastArguments.ToList().IndexOf("-format_whitelist");
        Assert.True(at >= 0);
        Assert.Equal("mp3", launcher.LastArguments[at + 1]);
        Assert.True(at < launcher.LastArguments.ToList().IndexOf("-i"), "the whitelist must come before the input");
    }

    [Fact]
    public async Task AFileThatIsNotTheTypeItClaimsIsRefusedByItsDemuxer()
    {
        // A concat list sent as audio/mpeg: the decoder is given the mp3 demuxer only, and reads nothing from it. The fake stands in
        // for that refusal with a failed exit; the argument check above shows the demuxer the real run is given.
        FakeDecoderLauncher launcher = new() { Behaviour = FakeDecoderLauncher.Mode.Fail };
        await using RunningServer server = await StartInstalledAsync(launcher);
        byte[] concat = "ffconcat version 1.0\nfile '/etc/hosts'\n"u8.ToArray();

        using HttpResponseMessage response = await DecoderTestSupport.UploadAsync(server, concat, "list.mp3", Mp3);

        // The fake is not ffmpeg, so this asserts the whitelist that a real ffmpeg is given; the refusal here is the fake's exit.
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        int at = launcher.LastArguments.ToList().IndexOf("-format_whitelist");
        Assert.True(at >= 0, "the process was not given a demuxer whitelist");
        Assert.Equal("mp3", launcher.LastArguments[at + 1]);
        Assert.Empty(DecoderTestSupport.TempFiles(server));
    }

    [Fact]
    public async Task MalformedCompressedFileIsRefusedAndLeavesNoTempFiles()
    {
        FakeDecoderLauncher launcher = new() { Behaviour = FakeDecoderLauncher.Mode.Fail };
        await using RunningServer server = await StartInstalledAsync(launcher);

        using HttpResponseMessage response = await DecoderTestSupport.UploadAsync(server, _compressedFile, "broken.mp3", Mp3);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains("not audio the server can read", await response.Content.ReadAsStringAsync());
        Assert.Equal(1, launcher.Starts);
        Assert.Empty(DecoderTestSupport.TempFiles(server));
    }

    [Fact]
    public async Task ADecodeThatRunsPastItsTimeLimitKillsTheRealProcessAndRemovesItsTempFiles()
    {
        FakeDecoderLauncher launcher = new() { Behaviour = FakeDecoderLauncher.Mode.HangProcess };
        await using RunningServer server = await StartInstalledAsync(launcher, TimeLimit("00:00:01"));

        using HttpResponseMessage response = await DecoderTestSupport.UploadAsync(server, _compressedFile, "slow.mp3", Mp3);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains("took longer", await response.Content.ReadAsStringAsync());
        Assert.Equal(1, launcher.Kills);
        int pid = Assert.IsType<int>(launcher.SpawnedProcessId);
        // A process that is still running would be found here; the kill has to have ended it, not only marked it.
        Assert.ThrowsAny<ArgumentException>(() => Process.GetProcessById(pid));
        Assert.Empty(DecoderTestSupport.TempFiles(server));
    }

    [Fact]
    public async Task ASilentlyHungDecodeIsRefusedAtItsTimeLimit()
    {
        FakeDecoderLauncher launcher = new() { Behaviour = FakeDecoderLauncher.Mode.Hang };
        await using RunningServer server = await StartInstalledAsync(launcher, TimeLimit("00:00:01"));

        using HttpResponseMessage response = await DecoderTestSupport.UploadAsync(server, _compressedFile, "slow.mp3", Mp3);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(1, launcher.Kills);
        Assert.Empty(DecoderTestSupport.TempFiles(server));
    }

    [Fact]
    public async Task AHostileFileNameIsKeptAsDataAndNeverReachesADecoderPath()
    {
        FakeDecoderLauncher launcher = new();
        await using RunningServer server = await StartInstalledAsync(launcher);

        using HttpResponseMessage response = await DecoderTestSupport.UploadAsync(server, _compressedFile, "../../escape.mp3", Mp3);

        // The name is a label on the media only. The decoder is given fixed temporary paths, so it never sees the name.
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(1, launcher.Starts);
        Assert.DoesNotContain(launcher.LastArguments, argument => argument.Contains("escape", StringComparison.Ordinal));
        Assert.Empty(Directory.GetFiles(server.StoreFolder, "escape*", SearchOption.AllDirectories));
        Assert.Empty(DecoderTestSupport.TempFiles(server));
    }

    [Fact]
    public async Task AnotherUsersHungDecodeDoesNotRefuseTheirUploads()
    {
        FakeDecoderLauncher launcher = new() { Behaviour = FakeDecoderLauncher.Mode.Hang };
        await using RunningServer server = await StartInstalledAsync(launcher, TimeLimit("00:00:02"));

        Task<HttpResponseMessage> mallory = DecoderTestSupport.UploadAsync(server, _compressedFile, "bad.mp3", Mp3, user: "mallory");
        Task<HttpResponseMessage> wav = DecoderTestSupport.UploadAsync(server, TestWav.Build(SampleEncoding.Pcm16, 2, 48000, 24000), "ok.wav", Wav);
        Task<HttpResponseMessage> alice = DecoderTestSupport.UploadAsync(server, _compressedFile, "good.mp3", Mp3);
        using HttpResponseMessage malloryAnswer = await mallory;
        using HttpResponseMessage wavAnswer = await wav;
        using HttpResponseMessage aliceAnswer = await alice;

        // Each user has one decode slot and the server two, so alice's decode runs beside mallory's and is not refused as busy.
        Assert.Equal(HttpStatusCode.UnprocessableEntity, malloryAnswer.StatusCode);
        Assert.Equal(HttpStatusCode.Created, wavAnswer.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, aliceAnswer.StatusCode);
        Assert.Empty(DecoderTestSupport.TempFiles(server));
    }

    [Fact]
    public async Task FiftyParallelCompressedUploadsOfOneUserDecodeOneAtATime()
    {
        FakeDecoderLauncher launcher = new() { Behaviour = FakeDecoderLauncher.Mode.Hang };
        await using RunningServer server = await StartInstalledAsync(launcher, TimeLimit("00:00:05"));

        Task<HttpResponseMessage>[] uploads = Enumerable.Range(0, 50)
            .Select(_ => DecoderTestSupport.UploadAsync(server, _compressedFile, "burst.mp3", Mp3))
            .ToArray();
        HttpResponseMessage[] answers = await Task.WhenAll(uploads);

        // Refused requests are answered before they spool, so the one decode that runs is the only file the server writes.
        Assert.Equal(1, launcher.Starts);
        Assert.Equal(1, server.Services.GetRequiredService<DecodedUploadService>().SpoolCount);
        Assert.Equal(49, answers.Count(answer => answer.StatusCode == HttpStatusCode.TooManyRequests));
        Assert.Empty(DecoderTestSupport.TempFiles(server));
        foreach (HttpResponseMessage answer in answers)
        {
            answer.Dispose();
        }
    }

    [Fact]
    public async Task AUploadThatArrivesWhileAnotherDecodesIsRefusedWith429()
    {
        FakeDecoderLauncher launcher = new() { Behaviour = FakeDecoderLauncher.Mode.Hang };
        await using RunningServer server = await StartInstalledAsync(launcher, TimeLimit("00:00:03"));

        Task<HttpResponseMessage> first = DecoderTestSupport.UploadAsync(server, _compressedFile, "first.mp3", Mp3);
        while (launcher.Starts == 0)
        {
            await Task.Delay(10);
        }
        using HttpResponseMessage second = await DecoderTestSupport.UploadAsync(server, _compressedFile, "second.mp3", Mp3);
        using HttpResponseMessage firstAnswer = await first;

        Assert.Equal(HttpStatusCode.TooManyRequests, second.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, firstAnswer.StatusCode);
        Assert.Equal(1, launcher.Starts);
    }

    [Fact]
    public async Task ACompressedFileOverTheInputLimitIsRefusedWith413BeforeItIsWritten()
    {
        FakeDecoderLauncher launcher = new();
        Dictionary<string, string?> settings = new() { ["AudioLab:Decoder:MaxInputBytes"] = "1024" };
        await using RunningServer server = await StartInstalledAsync(launcher, settings);

        using HttpResponseMessage response = await DecoderTestSupport.UploadAsync(server, new byte[4096], "big.mp3", Mp3);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Equal(0, launcher.Starts);
        Assert.Empty(DecoderTestSupport.TempFiles(server));
    }

    [Fact]
    public async Task ADecodedFileOverTheUploadLimitIsStoppedByTheDecoderAfterItExits()
    {
        // The fake writes past the limit and exits at once, so only the check after the exit can see it. The decoder stops the decode
        // itself, and the log says so, before the media service would have judged the size.
        FakeDecoderLauncher launcher = new() { Behaviour = FakeDecoderLauncher.Mode.Succeed };
        List<string> messages = [];
        byte[] archive = DecoderTestSupport.Archive(DecoderTestSupport.Binary);
        DecoderBuild build = DecoderTestSupport.BuildFor(archive, DecoderTestSupport.Binary);
        Dictionary<string, string?> settings = new() { ["AudioLab:Media:MaxUploadBytes"] = "1024" };
        await using RunningServer server = await DecoderTestSupport.StartAsync(
            build,
            new FakeDecoderDownloader(),
            launcher,
            settings,
            services => services.AddSingleton<ILogger<FfmpegDecoder>>(new CapturingLogger<FfmpegDecoder>(messages)));
        DecoderTestSupport.PlaceBinary(server, build, DecoderTestSupport.Binary);

        using HttpResponseMessage response = await DecoderTestSupport.UploadAsync(server, _compressedFile, "long.mp3", Mp3);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Single(messages, message => message == "Decode was stopped: the output limit");
        Assert.Empty(DecoderTestSupport.TempFiles(server));
    }

    [Fact]
    public async Task DecoderOutputThatIsNotWavIsRefusedWith422()
    {
        FakeDecoderLauncher launcher = new() { Behaviour = FakeDecoderLauncher.Mode.InvalidWav };
        await using RunningServer server = await StartInstalledAsync(launcher);

        using HttpResponseMessage response = await DecoderTestSupport.UploadAsync(server, _compressedFile, "odd.mp3", Mp3);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Empty(DecoderTestSupport.TempFiles(server));
    }

    [Fact]
    public async Task AFloodOfStderrIsReadAndOnlyAShortExcerptIsLogged()
    {
        FakeDecoderLauncher launcher = new() { Behaviour = FakeDecoderLauncher.Mode.NoisyStderr };
        List<string> messages = [];
        byte[] archive = DecoderTestSupport.Archive(DecoderTestSupport.Binary);
        DecoderBuild build = DecoderTestSupport.BuildFor(archive, DecoderTestSupport.Binary);
        await using RunningServer server = await DecoderTestSupport.StartAsync(
            build,
            new FakeDecoderDownloader(),
            launcher,
            new Dictionary<string, string?> { ["AudioLab:Decoder:Timeout"] = "00:00:10", ["AudioLab:Decoder:StderrCapBytes"] = "1024" },
            services => services.AddSingleton<ILogger<FfmpegDecoder>>(new CapturingLogger<FfmpegDecoder>(messages)));
        DecoderTestSupport.PlaceBinary(server, build, DecoderTestSupport.Binary);

        using HttpResponseMessage response = await DecoderTestSupport.UploadAsync(server, _compressedFile, "noisy.mp3", Mp3);

        // The read keeps going to the end of the error output, so the process never blocks; the log gets 200 characters of it.
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Empty(DecoderTestSupport.TempFiles(server));
        string logged = Assert.Single(messages, message => message.StartsWith("Decode exited", StringComparison.Ordinal));
        // The log has 200 characters of what was kept. It also says how much was kept: a read with no cap keeps all 200 KiB.
        Assert.Contains($"stderr: {new string('e', 200)} (kept 1024 of 204800 bytes)", logged, StringComparison.Ordinal);
        Assert.DoesNotContain(new string('e', 201), logged, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CompressedUploadIsRefusedWithItsReasonWhenNoDecoderIsInstalled()
    {
        FakeDecoderLauncher launcher = new();
        byte[] archive = DecoderTestSupport.Archive(DecoderTestSupport.Binary);
        DecoderBuild build = DecoderTestSupport.BuildFor(archive, DecoderTestSupport.Binary);
        await using RunningServer server = await DecoderTestSupport.StartAsync(build, new FakeDecoderDownloader(), launcher);

        using HttpResponseMessage response = await DecoderTestSupport.UploadAsync(server, _compressedFile, "song.mp3", Mp3);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("Install audio decoder", await response.Content.ReadAsStringAsync());
        Assert.Equal(0, launcher.Starts);
    }

    [Fact]
    public async Task OnAServerThatDoesNotOfferTheInstallTheRefusalSaysToUploadWav()
    {
        FakeDecoderLauncher launcher = new();
        byte[] archive = DecoderTestSupport.Archive(DecoderTestSupport.Binary);
        DecoderBuild build = DecoderTestSupport.BuildFor(archive, DecoderTestSupport.Binary);
        Dictionary<string, string?> settings = new() { ["AudioLab:Decoder:InstallEnabled"] = "false" };
        await using RunningServer server = await DecoderTestSupport.StartAsync(build, new FakeDecoderDownloader(), launcher, settings);

        using HttpResponseMessage response = await DecoderTestSupport.UploadAsync(server, _compressedFile, "song.mp3", Mp3);

        // Without the install button on this server, the refusal must not send the user to it.
        string body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("cannot be installed here", body);
        Assert.DoesNotContain("Install audio decoder", body);
        Assert.Equal(0, launcher.Starts);
    }

    [Fact]
    public async Task OnAPlatformWithNoBuildTheRefusalSaysTheDecoderIsNotAvailable()
    {
        FakeDecoderLauncher launcher = new();
        await using RunningServer server = await DecoderTestSupport.StartAsync(null, new FakeDecoderDownloader(), launcher);

        using HttpResponseMessage response = await DecoderTestSupport.UploadAsync(server, _compressedFile, "song.mp3", Mp3);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        string body = await response.Content.ReadAsStringAsync();
        Assert.Contains("not available on this system", body);
        Assert.DoesNotContain("Install audio decoder", body);
        Assert.Equal(0, launcher.Starts);
    }

    [Fact]
    public async Task ABinaryReplacedWithTheSameSizeAndTimeIsRefusedBeforeItRuns()
    {
        FakeDecoderLauncher launcher = new();
        byte[] archive = DecoderTestSupport.Archive(DecoderTestSupport.Binary);
        DecoderBuild build = DecoderTestSupport.BuildFor(archive, DecoderTestSupport.Binary);
        await using RunningServer server = await DecoderTestSupport.StartAsync(build, new FakeDecoderDownloader(), launcher);
        DecoderTestSupport.PlaceBinary(server, build, DecoderTestSupport.Binary);
        using (HttpResponseMessage good = await DecoderTestSupport.UploadAsync(server, _compressedFile, "good.mp3", Mp3))
        {
            Assert.Equal(HttpStatusCode.Created, good.StatusCode);
        }
        string path = DecoderTestSupport.BinaryPath(server, build);
        DateTime written = File.GetLastWriteTimeUtc(path);

        // Same length, same write time, different bytes: only a hash on every decode sees the change.
        DecoderTestSupport.PlaceBinary(server, build, Enumerable.Repeat((byte)'x', DecoderTestSupport.Binary.Length).ToArray());
        File.SetLastWriteTimeUtc(path, written);
        using HttpResponseMessage response = await DecoderTestSupport.UploadAsync(server, _compressedFile, "song.mp3", Mp3);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("does not match its pinned checksum", await response.Content.ReadAsStringAsync());
        Assert.Equal(1, launcher.Starts);
    }

    [Fact]
    public async Task AMissingTemporaryFolderIsARefusalWithItsReasonNotAServerError()
    {
        FakeDecoderLauncher launcher = new();
        await using RunningServer server = await StartInstalledAsync(launcher);
        string temp = server.Services.GetRequiredService<DecoderLocation>().TempFolder;
        Directory.Delete(temp, recursive: true);
        // A file where the folder belongs: the folder cannot be made again, so the refusal must come from the check, not from a retry.
        File.WriteAllText(temp, "not a folder");

        using HttpResponseMessage response = await DecoderTestSupport.UploadAsync(server, _compressedFile, "song.mp3", Mp3);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("temporary folder", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Equal(0, launcher.Starts);
    }
}
