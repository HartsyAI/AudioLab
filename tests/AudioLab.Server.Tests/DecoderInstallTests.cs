using System.Net;
using System.Text.Json;
using AudioLab.Core.Api.Decoder;
using AudioLab.Server.Decoding;

namespace AudioLab.Server.Tests;

public sealed class DecoderInstallTests
{
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
    public async Task ReportsNoBuildForThisPlatformWithoutDownloading()
    {
        FakeDecoderDownloader downloader = new() { Payload = DecoderTestSupport.Archive(DecoderTestSupport.Binary) };
        await using RunningServer server = await DecoderTestSupport.StartAsync(null, downloader, new FakeDecoderLauncher());

        using HttpResponseMessage response = await DecoderTestSupport.PostInstallAsync(server, DecoderTestSupport.Version, new string('0', 64));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(0, downloader.Calls);
    }

    [Fact]
    public async Task AnInstalledBinaryThatNoLongerMatchesIsNotCountedAsInstalled()
    {
        byte[] archive = DecoderTestSupport.Archive(DecoderTestSupport.Binary);
        DecoderBuild build = DecoderTestSupport.BuildFor(archive, DecoderTestSupport.Binary);
        await using RunningServer server = await DecoderTestSupport.StartAsync(build, new FakeDecoderDownloader { Payload = archive }, new FakeDecoderLauncher());
        DecoderTestSupport.PlaceBinary(server, build, "edited on disk"u8.ToArray());

        using HttpRequestMessage request = DecoderTestSupport.Request(HttpMethod.Get, "/api/decoder");
        using HttpResponseMessage response = await server.Client.SendAsync(request);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.False(document.RootElement.GetProperty("installed").GetBoolean());
        Assert.Contains("pinned checksum", document.RootElement.GetProperty("reason").GetString());
    }
}
