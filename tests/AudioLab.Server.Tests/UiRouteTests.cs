using System.Net;

namespace AudioLab.Server.Tests;

public sealed class UiRouteTests
{
    [Fact]
    public async Task TheStartPageIsServedAtTheRootAndForAnyClientSideAddress()
    {
        await using RunningServer server = await RunningServer.StartAsync();

        foreach (string address in new[] { "/", "/project/song-1", "/projects/a/b" })
        {
            using HttpResponseMessage response = await server.Client.GetAsync(address);
            string page = await response.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
            Assert.Contains("<base href=\"/\" />", page, StringComparison.Ordinal);
            Assert.Contains("_framework/blazor.webassembly.js", page, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task ThePathPrefixIsWrittenIntoTheBaseTagSoTheAppResolvesItsAddressesUnderIt()
    {
        await using RunningServer server = await RunningServer.StartAsync(new Dictionary<string, string?> { ["AudioLab:PathBase"] = "/audiolab" });

        string page = await server.Client.GetStringAsync("/audiolab/project/song-1");

        Assert.Contains("<base href=\"/audiolab/\" />", page, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheFrameworkAndTheStylesAreServedAsFiles()
    {
        await using RunningServer server = await RunningServer.StartAsync();

        using HttpResponseMessage script = await server.Client.GetAsync("/_framework/blazor.webassembly.js");
        using HttpResponseMessage tokens = await server.Client.GetAsync("/css/tokens.css");
        using HttpResponseMessage font = await server.Client.GetAsync("/fonts/inter-latin-400-normal.woff2");

        Assert.Equal(HttpStatusCode.OK, script.StatusCode);
        Assert.Equal(HttpStatusCode.OK, tokens.StatusCode);
        Assert.Equal(HttpStatusCode.OK, font.StatusCode);
    }

    [Fact]
    public async Task AnUnknownApiAddressOrMissingFileIsNotFoundAndNeverTheStartPage()
    {
        await using RunningServer server = await RunningServer.StartAsync();

        Assert.Equal(HttpStatusCode.NotFound, (await server.Client.GetAsync("/api/nothing")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await server.Client.GetAsync("/_framework/missing.js")).StatusCode);
        // An address with a dot is a file, never a client-side route: no valid id holds a dot, so none is lost.
        Assert.Equal(HttpStatusCode.NotFound, (await server.Client.GetAsync("/project/song.1")).StatusCode);
    }

    [Theory]
    [InlineData("/_framework/blazor.server.js")]
    [InlineData("/_framework/blazor.web.js")]
    public async Task TheScriptsOfOtherBlazorHostingModelsAreNotServed(string address)
    {
        await using RunningServer server = await RunningServer.StartAsync();

        Assert.Equal(HttpStatusCode.NotFound, (await server.Client.GetAsync(address)).StatusCode);
    }

    [Fact]
    public async Task TheProgramIsServedWithoutADoorButTheProjectsAreNot()
    {
        Dictionary<string, string?> settings = new()
        {
            ["AudioLab:Door:Kind"] = "Hosted",
            ["AudioLab:Door:ProxySecret"] = new string('s', 40),
        };
        await using RunningServer server = await RunningServer.StartAsync(settings);

        Assert.Equal(HttpStatusCode.OK, (await server.Client.GetAsync("/")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await server.Client.GetAsync("/api/projects")).StatusCode);
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/project/song-1")]
    [InlineData("/css/tokens.css")]
    [InlineData("/_framework/blazor.webassembly.js")]
    public async Task TheProgramForbidsFramingByAnotherOriginAndSniffing(string address)
    {
        await using RunningServer server = await RunningServer.StartAsync();

        using HttpResponseMessage response = await server.Client.GetAsync(address);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("frame-ancestors 'self'", Assert.Single(response.Headers.GetValues("Content-Security-Policy")));
        Assert.Equal("SAMEORIGIN", Assert.Single(response.Headers.GetValues("X-Frame-Options")));
        Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
    }

    [Fact]
    public async Task TheRoutesOfProjectsDoNotCarryTheFramingHeaders()
    {
        await using RunningServer server = await RunningServer.StartAsync();

        using HttpResponseMessage response = await server.Client.GetAsync("/api/projects");

        Assert.False(response.Headers.Contains("X-Frame-Options"));
    }
}
