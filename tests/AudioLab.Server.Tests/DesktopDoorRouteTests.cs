using System.Net;
using System.Net.Http.Json;
using AudioLab.Server.Json;
using AudioLab.Server.Services;

namespace AudioLab.Server.Tests;

public sealed class DesktopDoorRouteTests
{
    [Fact]
    public async Task AWebPageOnAnotherSiteCannotActAsTheLocalUser()
    {
        await using RunningServer server = await RunningServer.StartAsync();
        string own = server.Client.BaseAddress!.GetLeftPart(UriPartial.Authority);

        Assert.Equal(HttpStatusCode.OK, (await Send(server, HttpMethod.Get, null, null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Send(server, HttpMethod.Get, own, "same-origin")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(server, HttpMethod.Get, "http://evil.example", "cross-site")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(server, HttpMethod.Get, "http://evil.example", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(server, HttpMethod.Get, null, "same-site")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(server, HttpMethod.Get, "null", null)).StatusCode);
    }

    [Fact]
    public async Task ARebindingPageThatReachesTheServerUnderAForeignHostNameIsRefused()
    {
        await using RunningServer server = await RunningServer.StartAsync();
        using HttpRequestMessage request = new(HttpMethod.Get, "/api/session");
        request.Headers.Host = "rebind.evil.example";
        Assert.Equal(HttpStatusCode.Forbidden, (await server.Client.SendAsync(request)).StatusCode);
        SessionInfo? session = await server.Client.GetFromJsonAsync("/api/session", ServerJsonContext.Default.SessionInfo);
        Assert.Equal("desktop-local", session?.User);
    }

    private static async Task<HttpResponseMessage> Send(RunningServer server, HttpMethod method, string? origin, string? site)
    {
        using HttpRequestMessage request = new(method, "/api/session");
        if (origin is not null)
        {
            request.Headers.Add("Origin", origin);
        }
        if (site is not null)
        {
            request.Headers.Add("Sec-Fetch-Site", site);
        }
        return await server.Client.SendAsync(request);
    }
}
