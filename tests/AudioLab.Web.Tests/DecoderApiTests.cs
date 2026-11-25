using System.Net;
using System.Text.Json;
using AudioLab.Core.Api.Decoder;
using AudioLab.Web.Services;

namespace AudioLab.Web.Tests;

/// <summary>Holds the decoder api tests.</summary>
public sealed class DecoderApiTests
{
    private const string Sha = "60f467265b1e312373dbcd92200c2618a74850f98d3d078e94296bb3fa2047ba";

    private const string Status =
        "{\"supported\":true,\"installed\":false,\"installedVersion\":null,\"phase\":\"idle\",\"received\":0,\"total\":114768076,"
        + "\"reason\":null,\"package\":{\"version\":\"9.0.2\","
        + "\"sourceUrl\":\"https://www.gyan.dev/ffmpeg/builds/packages/ffmpeg-9.0.2-essentials_build.zip\","
        + "\"bytes\":114768076,\"sha256\":\"" + Sha + "\"},\"installEnabled\":true}";

    private static readonly Uri _site = new("http://audiolab.test/audiolab/");

    private static DecoderApi Api(HttpMessageHandler handler) => new(new HttpClient(handler, false) { BaseAddress = _site });

    /// <summary>The status is read from its route under the path prefix, with the build the user would confirm.</summary>
    [Fact]
    public async Task TheStatusIsReadFromTheRouteWithTheBuildToConfirm()
    {
        StubHandler handler = new(HttpStatusCode.OK, Status);

        DecoderStatus status = await Api(handler).StatusAsync();

        Assert.Equal("/audiolab/api/decoder", handler.Requests.Single().Path);
        Assert.True(status.Supported);
        Assert.False(status.Installed);
        Assert.NotNull(status.Package);
        Assert.Equal(("9.0.2", Sha), (status.Package.Version, status.Package.Sha256));
        Assert.True(status.InstallEnabled);
    }

    /// <summary>The install names exactly the version and checksum the user confirmed, and nothing else.</summary>
    [Fact]
    public async Task TheInstallSendsTheConfirmedVersionAndChecksum()
    {
        StubHandler handler = new(HttpStatusCode.Accepted, Status);
        DecoderPackage package = JsonSerializer.Deserialize(
            "{\"version\":\"9.0.2\",\"sourceUrl\":\"https://www.gyan.dev/x.zip\",\"bytes\":1,\"sha256\":\"" + Sha + "\"}",
            DecoderJsonContext.Default.DecoderPackage)!;

        await Api(handler).InstallAsync(package);

        (HttpMethod method, string path, string body) = handler.Requests.Single();
        Assert.Equal(HttpMethod.Post, method);
        Assert.Equal("/audiolab/api/decoder/install", path);
        Assert.Contains("\"version\":\"9.0.2\"", body, StringComparison.Ordinal);
        Assert.Contains("\"sha256\":\"" + Sha + "\"", body, StringComparison.Ordinal);
    }

    /// <summary>A refused install is an <see cref="ApiException"/> with the server's reason, never a parser error.</summary>
    [Fact]
    public async Task ARefusedInstallIsAnApiException()
    {
        StubHandler handler = new(
            HttpStatusCode.UnprocessableEntity,
            "{\"title\":\"The version or checksum does not match the build shown. Reload the page and confirm again.\",\"status\":422}",
            "application/problem+json");
        DecoderPackage package = JsonSerializer.Deserialize(
            "{\"version\":\"9.0.2\",\"sourceUrl\":\"https://www.gyan.dev/x.zip\",\"bytes\":1,\"sha256\":\"" + Sha + "\"}",
            DecoderJsonContext.Default.DecoderPackage)!;

        ApiException exception = await Assert.ThrowsAsync<ApiException>(() => Api(handler).InstallAsync(package));

        Assert.Contains("does not match the build shown", exception.Message, StringComparison.Ordinal);
    }
}
