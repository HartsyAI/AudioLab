using System.Net;
using AudioLab.Audio.Tests.Support;
using AudioLab.Core.Model;
using AudioLab.Server.Decoding;

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
    public async Task ADecodeThatRunsPastItsTimeLimitIsKilledAndItsTempFilesRemoved()
    {
        FakeDecoderLauncher launcher = new() { Behaviour = FakeDecoderLauncher.Mode.Hang };
        Dictionary<string, string?> settings = new() { ["AudioLab:Decoder:Timeout"] = "00:00:01" };
        await using RunningServer server = await StartInstalledAsync(launcher, settings);

        using HttpResponseMessage response = await DecoderTestSupport.UploadAsync(server, _compressedFile, "slow.mp3", Mp3);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains("took longer", await response.Content.ReadAsStringAsync());
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
    public async Task AnotherUsersFailedDecodeDoesNotTouchTheirUploads()
    {
        FakeDecoderLauncher launcher = new() { Behaviour = FakeDecoderLauncher.Mode.Fail };
        await using RunningServer server = await StartInstalledAsync(launcher);

        using HttpResponseMessage failed = await DecoderTestSupport.UploadAsync(server, _compressedFile, "bad.mp3", Mp3, user: "mallory");
        using HttpResponseMessage wav = await DecoderTestSupport.UploadAsync(server, TestWav.Build(SampleEncoding.Pcm16, 2, 48000, 24000), "ok.wav", Wav);
        launcher.Behaviour = FakeDecoderLauncher.Mode.Succeed;
        using HttpResponseMessage decoded = await DecoderTestSupport.UploadAsync(server, _compressedFile, "good.mp3", Mp3);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, failed.StatusCode);
        Assert.Equal(HttpStatusCode.Created, wav.StatusCode);
        Assert.Equal(HttpStatusCode.Created, decoded.StatusCode);
        Assert.Empty(DecoderTestSupport.TempFiles(server));
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
}
