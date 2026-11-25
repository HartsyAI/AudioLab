using System.Net;
using System.Text.Json;
using AudioLab.Core.Api.Decoder;
using AudioLab.Server.Decoding;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AudioLab.Server.Tests;

public sealed class DecoderInstallTests
{
    private const string Limit = "AudioLab:Decoder:InstallTimeout";

    [Fact]
    public async Task InstallsTheConfirmedBuildAndExtractsOnlyTheVerifiedBinary()
    {
        byte[] archive = DecoderTestSupport.Archive(DecoderTestSupport.Binary);
        DecoderBuild build = DecoderTestSupport.BuildFor(archive, DecoderTestSupport.Binary);
        FakeDecoderDownloader downloader = new() { Payload = archive };
        await using RunningServer server = await DecoderTestSupport.StartAsync(build, downloader, new FakeDecoderLauncher());

        using HttpResponseMessage accepted = await DecoderTestSupport.PostInstallAsync(server, build.Package.Version, build.Package.Sha256);

        Assert.Equal(HttpStatusCode.Accepted, accepted.StatusCode);
        JsonElement status = await DecoderTestSupport.WaitForPhaseAsync(server, DecoderPhases.Installed, DecoderPhases.Failed);
        Assert.Equal(DecoderPhases.Installed, status.GetProperty("phase").GetString());
        Assert.True(status.GetProperty("installed").GetBoolean());
        Assert.Equal(1, downloader.Calls);
        Assert.Equal(DecoderTestSupport.Binary, await File.ReadAllBytesAsync(DecoderTestSupport.BinaryPath(server, build)));
        Assert.Empty(DecoderTestSupport.TempFiles(server));
    }

    [Fact]
    public async Task RefusesAnArchiveThatDoesNotMatchItsChecksumAndInstallsNothing()
    {
        byte[] archive = DecoderTestSupport.Archive(DecoderTestSupport.Binary);
        DecoderBuild build = DecoderTestSupport.BuildFor(archive, DecoderTestSupport.Binary);
        // Same length as the pinned archive, one byte changed: only the checksum can tell them apart.
        byte[] tampered = (byte[])archive.Clone();
        tampered[^1] ^= 0xFF;
        FakeDecoderDownloader downloader = new() { Payload = tampered };
        await using RunningServer server = await DecoderTestSupport.StartAsync(build, downloader, new FakeDecoderLauncher());

        using HttpResponseMessage accepted = await DecoderTestSupport.PostInstallAsync(server, build.Package.Version, build.Package.Sha256);
        Assert.Equal(HttpStatusCode.Accepted, accepted.StatusCode);
        JsonElement status = await DecoderTestSupport.WaitForPhaseAsync(server, DecoderPhases.Installed, DecoderPhases.Failed);

        Assert.Equal(DecoderPhases.Failed, status.GetProperty("phase").GetString());
        Assert.Contains("archive does not match", status.GetProperty("reason").GetString());
        Assert.False(File.Exists(DecoderTestSupport.BinaryPath(server, build)));
        Assert.Empty(DecoderTestSupport.TempFiles(server));
    }

    [Fact]
    public async Task RefusesABinaryThatDoesNotMatchItsPinnedChecksum()
    {
        byte[] archive = DecoderTestSupport.Archive(DecoderTestSupport.Binary);
        DecoderBuild good = DecoderTestSupport.BuildFor(archive, DecoderTestSupport.Binary);
        // The archive is genuine, but the binary inside it is not the one this build pins.
        DecoderBuild build = good with { BinarySha256 = DecoderTestSupport.Sha256("some other binary"u8.ToArray()) };
        FakeDecoderDownloader downloader = new() { Payload = archive };
        await using RunningServer server = await DecoderTestSupport.StartAsync(build, downloader, new FakeDecoderLauncher());

        using HttpResponseMessage accepted = await DecoderTestSupport.PostInstallAsync(server, build.Package.Version, build.Package.Sha256);
        Assert.Equal(HttpStatusCode.Accepted, accepted.StatusCode);
        JsonElement status = await DecoderTestSupport.WaitForPhaseAsync(server, DecoderPhases.Installed, DecoderPhases.Failed);

        Assert.Equal(DecoderPhases.Failed, status.GetProperty("phase").GetString());
        Assert.Contains("binary does not match", status.GetProperty("reason").GetString());
        Assert.False(File.Exists(DecoderTestSupport.BinaryPath(server, build)));
        Assert.Empty(DecoderTestSupport.TempFiles(server));
    }

    [Fact]
    public async Task DownloadsNothingForARequestThatDoesNotNameTheShownBuild()
    {
        byte[] archive = DecoderTestSupport.Archive(DecoderTestSupport.Binary);
        DecoderBuild build = DecoderTestSupport.BuildFor(archive, DecoderTestSupport.Binary);
        FakeDecoderDownloader downloader = new() { Payload = archive };
        await using RunningServer server = await DecoderTestSupport.StartAsync(build, downloader, new FakeDecoderLauncher());

        using HttpResponseMessage response = await DecoderTestSupport.PostInstallAsync(server, build.Package.Version, new string('0', 64));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(0, downloader.Calls);
    }

    [Fact]
    public async Task ReportsNoBuildForThisPlatformAndDownloadsNothing()
    {
        FakeDecoderDownloader downloader = new() { Payload = DecoderTestSupport.Archive(DecoderTestSupport.Binary) };
        await using RunningServer server = await DecoderTestSupport.StartAsync(null, downloader, new FakeDecoderLauncher());

        using HttpResponseMessage response = await DecoderTestSupport.PostInstallAsync(server, DecoderTestSupport.Version, new string('0', 64));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("not available on this system", await response.Content.ReadAsStringAsync());
        Assert.Equal(0, downloader.Calls);
    }

    [Fact]
    public async Task AnInstalledBinaryThatNoLongerMatchesIsNotCountedAsInstalled()
    {
        byte[] archive = DecoderTestSupport.Archive(DecoderTestSupport.Binary);
        DecoderBuild build = DecoderTestSupport.BuildFor(archive, DecoderTestSupport.Binary);
        FakeDecoderDownloader downloader = new() { Payload = archive };
        await using RunningServer server = await DecoderTestSupport.StartAsync(build, downloader, new FakeDecoderLauncher());
        DecoderTestSupport.PlaceBinary(server, build, "edited on disk"u8.ToArray());

        using HttpRequestMessage request = DecoderTestSupport.Request(HttpMethod.Get, "/api/decoder");
        using HttpResponseMessage response = await server.Client.SendAsync(request);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.False(document.RootElement.GetProperty("installed").GetBoolean());
        Assert.Contains("pinned checksum", document.RootElement.GetProperty("reason").GetString());
    }

    [Fact]
    public async Task ADisabledInstallIsRefusedWith403AndDownloadsNothing()
    {
        byte[] archive = DecoderTestSupport.Archive(DecoderTestSupport.Binary);
        DecoderBuild build = DecoderTestSupport.BuildFor(archive, DecoderTestSupport.Binary);
        FakeDecoderDownloader downloader = new() { Payload = archive };
        Dictionary<string, string?> settings = new() { ["AudioLab:Decoder:InstallEnabled"] = "false" };
        await using RunningServer server = await DecoderTestSupport.StartAsync(build, downloader, new FakeDecoderLauncher(), settings);

        using HttpResponseMessage response = await DecoderTestSupport.PostInstallAsync(server, build.Package.Version, build.Package.Sha256);
        using HttpRequestMessage status = DecoderTestSupport.Request(HttpMethod.Get, "/api/decoder");
        using HttpResponseMessage answer = await server.Client.SendAsync(status);
        using JsonDocument document = JsonDocument.Parse(await answer.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(0, downloader.Calls);
        Assert.False(document.RootElement.GetProperty("installEnabled").GetBoolean());
    }

    [Fact]
    public async Task AShareDoorDoesNotOfferTheInstallUnlessTheConfigurationTurnsItOn()
    {
        Dictionary<string, string?> settings = new()
        {
            ["AudioLab:Door:Kind"] = "Swarm",
            ["AudioLab:Door:ProxySecret"] = "0123456789abcdef0123456789abcdef",
        };
        await using RunningServer server = await RunningServer.StartAsync(settings);

        using HttpRequestMessage request = DecoderTestSupport.Request(HttpMethod.Get, "/api/decoder");
        using HttpResponseMessage response = await server.Client.SendAsync(request);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.False(document.RootElement.GetProperty("installEnabled").GetBoolean());
    }

    [Fact]
    public async Task TheDesktopDoorOffersTheInstallByDefault()
    {
        await using RunningServer server = await RunningServer.StartAsync();

        using HttpRequestMessage request = DecoderTestSupport.Request(HttpMethod.Get, "/api/decoder");
        using HttpResponseMessage response = await server.Client.SendAsync(request);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.True(document.RootElement.GetProperty("installEnabled").GetBoolean());
    }

    [Fact]
    public async Task ASourceOutsideTheBuildsPinnedHostsIsRefused()
    {
        byte[] archive = DecoderTestSupport.Archive(DecoderTestSupport.Binary);
        DecoderBuild build = DecoderTestSupport.BuildFor(archive, DecoderTestSupport.Binary) with { Hosts = ["www.gyan.dev"] };
        FakeDecoderDownloader downloader = new() { Payload = archive };
        await using RunningServer server = await DecoderTestSupport.StartAsync(build, downloader, new FakeDecoderLauncher());

        using HttpResponseMessage accepted = await DecoderTestSupport.PostInstallAsync(server, build.Package.Version, build.Package.Sha256);
        Assert.Equal(HttpStatusCode.Accepted, accepted.StatusCode);
        JsonElement status = await DecoderTestSupport.WaitForPhaseAsync(server, DecoderPhases.Installed, DecoderPhases.Failed);

        Assert.Equal(DecoderPhases.Failed, status.GetProperty("phase").GetString());
        Assert.Contains("not one this build may use", status.GetProperty("reason").GetString());
    }

    [Fact]
    public async Task AStalledDownloadFailsAtTheInstallTimeoutAndLeavesNothingBehind()
    {
        byte[] archive = DecoderTestSupport.Archive(DecoderTestSupport.Binary);
        DecoderBuild build = DecoderTestSupport.BuildFor(archive, DecoderTestSupport.Binary);
        FakeDecoderDownloader downloader = new() { Payload = archive, Behaviour = FakeDecoderDownloader.Mode.Stall };
        Dictionary<string, string?> settings = new() { [Limit] = "00:00:02" };
        await using RunningServer server = await DecoderTestSupport.StartAsync(build, downloader, new FakeDecoderLauncher(), settings);

        using HttpResponseMessage accepted = await DecoderTestSupport.PostInstallAsync(server, build.Package.Version, build.Package.Sha256);
        Assert.Equal(HttpStatusCode.Accepted, accepted.StatusCode);
        JsonElement status = await DecoderTestSupport.WaitForPhaseAsync(server, DecoderPhases.Failed);

        Assert.Contains("took longer than the server allows", status.GetProperty("reason").GetString());
        Assert.Equal(1, downloader.Calls);
        Assert.Empty(DecoderTestSupport.TempFiles(server));
    }

    [Fact]
    public async Task ASecondInstallWhileOneRunsIsRefusedWith409()
    {
        byte[] archive = DecoderTestSupport.Archive(DecoderTestSupport.Binary);
        DecoderBuild build = DecoderTestSupport.BuildFor(archive, DecoderTestSupport.Binary);
        FakeDecoderDownloader downloader = new() { Payload = archive, Behaviour = FakeDecoderDownloader.Mode.Stall };
        Dictionary<string, string?> settings = new() { [Limit] = "00:00:03" };
        await using RunningServer server = await DecoderTestSupport.StartAsync(build, downloader, new FakeDecoderLauncher(), settings);

        using HttpResponseMessage first = await DecoderTestSupport.PostInstallAsync(server, build.Package.Version, build.Package.Sha256);
        using HttpResponseMessage second = await DecoderTestSupport.PostInstallAsync(server, build.Package.Version, build.Package.Sha256);

        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Contains("already running", await second.Content.ReadAsStringAsync());
        await DecoderTestSupport.WaitForPhaseAsync(server, DecoderPhases.Failed);
    }

    [Fact]
    public async Task AnUnlistedFailureStillEndsTheInstallAsFailed()
    {
        byte[] archive = DecoderTestSupport.Archive(DecoderTestSupport.Binary);
        DecoderBuild build = DecoderTestSupport.BuildFor(archive, DecoderTestSupport.Binary);
        FakeDecoderDownloader downloader = new() { Payload = archive, Behaviour = FakeDecoderDownloader.Mode.Throw };
        List<string> messages = [];
        await using RunningServer server = await DecoderTestSupport.StartAsync(
            build,
            downloader,
            new FakeDecoderLauncher(),
            extra: services => services.AddSingleton<ILogger<DecoderInstaller>>(new CapturingLogger<DecoderInstaller>(messages)));

        using HttpResponseMessage accepted = await DecoderTestSupport.PostInstallAsync(server, build.Package.Version, build.Package.Sha256);
        Assert.Equal(HttpStatusCode.Accepted, accepted.StatusCode);
        JsonElement status = await DecoderTestSupport.WaitForPhaseAsync(server, DecoderPhases.Failed);

        Assert.Contains("stopped unexpectedly", status.GetProperty("reason").GetString());
        Assert.Empty(DecoderTestSupport.TempFiles(server));
        // Logged once, with the exception's type; the exception itself still ends the install as a failure.
        Assert.Single(
            messages,
            message => message.StartsWith("Decoder install stopped unexpectedly: InvalidOperationException", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AnArchiveEntryThatClimbsOutOfTheFolderIsNeverWritten()
    {
        string escape = $"audiolab-zip-slip-{Guid.NewGuid():N}.txt";
        byte[] archive = DecoderTestSupport.Archive(
            DecoderTestSupport.Binary,
            ($"../{escape}", "written outside the decoder folder"u8.ToArray()));
        DecoderBuild build = DecoderTestSupport.BuildFor(archive, DecoderTestSupport.Binary);
        FakeDecoderDownloader downloader = new() { Payload = archive };
        await using RunningServer server = await DecoderTestSupport.StartAsync(build, downloader, new FakeDecoderLauncher());

        using HttpResponseMessage accepted = await DecoderTestSupport.PostInstallAsync(server, build.Package.Version, build.Package.Sha256);
        Assert.Equal(HttpStatusCode.Accepted, accepted.StatusCode);
        JsonElement status = await DecoderTestSupport.WaitForPhaseAsync(server, DecoderPhases.Installed, DecoderPhases.Failed);

        Assert.Equal(DecoderPhases.Installed, status.GetProperty("phase").GetString());
        Assert.False(File.Exists(Path.GetFullPath(Path.Combine(server.StoreFolder, "..", escape))));
        Assert.Empty(Directory.GetFiles(server.StoreFolder, escape, SearchOption.AllDirectories));
    }

    [Fact]
    public async Task OnlyTheNamedBinaryIsLeftInTheDecoderFolder()
    {
        byte[] archive = DecoderTestSupport.Archive(
            DecoderTestSupport.Binary,
            ("bin/tool.exe", "an extra program"u8.ToArray()),
            ("README.txt", "not the decoder"u8.ToArray()));
        DecoderBuild build = DecoderTestSupport.BuildFor(archive, DecoderTestSupport.Binary);
        FakeDecoderDownloader downloader = new() { Payload = archive };
        await using RunningServer server = await DecoderTestSupport.StartAsync(build, downloader, new FakeDecoderLauncher());

        using HttpResponseMessage accepted = await DecoderTestSupport.PostInstallAsync(server, build.Package.Version, build.Package.Sha256);
        Assert.Equal(HttpStatusCode.Accepted, accepted.StatusCode);
        JsonElement status = await DecoderTestSupport.WaitForPhaseAsync(server, DecoderPhases.Installed, DecoderPhases.Failed);

        Assert.Equal(DecoderPhases.Installed, status.GetProperty("phase").GetString());
        string folder = server.Services.GetRequiredService<DecoderLocation>().Folder;
        string[] files = Directory.GetFiles(folder, "*", SearchOption.AllDirectories);
        Assert.Equal(new[] { DecoderTestSupport.BinaryPath(server, build) }, files);
    }

    [Fact]
    public async Task StatusHashesAnUnchangedBinaryOnceAndHashesAgainAfterItChanges()
    {
        byte[] archive = DecoderTestSupport.Archive(DecoderTestSupport.Binary);
        DecoderBuild build = DecoderTestSupport.BuildFor(archive, DecoderTestSupport.Binary);
        await using RunningServer server = await DecoderTestSupport.StartAsync(build, new FakeDecoderDownloader(), new FakeDecoderLauncher());
        DecoderTestSupport.PlaceBinary(server, build, DecoderTestSupport.Binary);
        DecoderInstaller installer = server.Services.GetRequiredService<DecoderInstaller>();

        Assert.True(await StatusInstalledAsync(server));
        int afterFirst = installer.HashCount;
        Assert.True(await StatusInstalledAsync(server));
        Assert.Equal(afterFirst, installer.HashCount);

        // Same length, different bytes, a later write time: the status must look again and report the mismatch.
        string path = DecoderTestSupport.BinaryPath(server, build);
        DecoderTestSupport.PlaceBinary(server, build, Enumerable.Repeat((byte)'x', DecoderTestSupport.Binary.Length).ToArray());
        File.SetLastWriteTimeUtc(path, File.GetLastWriteTimeUtc(path).AddMinutes(1));
        Assert.False(await StatusInstalledAsync(server));
        Assert.Equal(afterFirst + 1, installer.HashCount);
    }

    private static async Task<bool> StatusInstalledAsync(RunningServer server)
    {
        using HttpRequestMessage request = DecoderTestSupport.Request(HttpMethod.Get, "/api/decoder");
        using HttpResponseMessage response = await server.Client.SendAsync(request);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("installed").GetBoolean();
    }
}
