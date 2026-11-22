using System.Net;
using System.Net.Http.Json;
using AudioLab.Core;
using AudioLab.Core.Model;
using AudioLab.Server.Doors;
using AudioLab.Server.Json;
using AudioLab.Server.Services;

namespace AudioLab.Server.Tests;

public sealed class RouteTests
{
    private const string Secret = "0123456789abcdef0123456789abcdef";

    [Fact]
    public async Task HealthAnswersWithoutAUserAndNamesTheContractAndDoor()
    {
        await using RunningServer server = await RunningServer.StartAsync();
        HealthReport? report = await server.Client.GetFromJsonAsync("/health", ServerJsonContext.Default.HealthReport);
        Assert.Equal(new HealthReport("ok", AudioLabApi.Version, "desktop"), report);
    }

    [Fact]
    public async Task EverythingWorksUnderAPathPrefixAndWithoutIt()
    {
        await using RunningServer server = await RunningServer.StartAsync(new Dictionary<string, string?> { ["AudioLab:PathBase"] = "/audiolab" });
        Assert.Equal(HttpStatusCode.OK, (await server.Client.GetAsync("/audiolab/health")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await server.Client.GetAsync("/health")).StatusCode);
        SessionInfo? session = await server.Client.GetFromJsonAsync("/audiolab/api/session", ServerJsonContext.Default.SessionInfo);
        Assert.Equal(new SessionInfo("desktop", UserId.Local.Value), session);
    }

    [Fact]
    public async Task TheDesktopDoorNamesTheLocalUser()
    {
        await using RunningServer server = await RunningServer.StartAsync();
        SessionInfo? session = await server.Client.GetFromJsonAsync("/api/session", ServerJsonContext.Default.SessionInfo);
        Assert.Equal(new SessionInfo("desktop", "desktop-local"), session);
    }

    [Fact]
    public async Task TheSwarmDoorTakesTheUserFromTheProxy()
    {
        await using RunningServer server = await RunningServer.StartAsync(SwarmSettings());
        using HttpRequestMessage request = Proxied("/api/session", "alice");
        using HttpResponseMessage response = await server.Client.SendAsync(request);
        SessionInfo? session = await response.Content.ReadFromJsonAsync(ServerJsonContext.Default.SessionInfo);
        Assert.Equal(new SessionInfo("swarm", ProxyDoor.UserFor("swarm", "alice").Value), session);
    }

    [Fact]
    public async Task TheSwarmDoorRefusesARequestThatDidNotComeThroughItsProxy()
    {
        await using RunningServer server = await RunningServer.StartAsync(SwarmSettings());
        using HttpRequestMessage noSecret = new(HttpMethod.Get, "/api/session");
        noSecret.Headers.Add("X-AudioLab-User", "alice");
        Assert.Equal(HttpStatusCode.Forbidden, (await server.Client.SendAsync(noSecret)).StatusCode);
        using HttpRequestMessage wrongSecret = Proxied("/api/session", "alice", "0123456789abcdef0123456789abcdee");
        Assert.Equal(HttpStatusCode.Forbidden, (await server.Client.SendAsync(wrongSecret)).StatusCode);
        using HttpRequestMessage noUser = Proxied("/api/session", null);
        Assert.Equal(HttpStatusCode.Unauthorized, (await server.Client.SendAsync(noUser)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await server.Client.GetAsync("/health")).StatusCode);
    }

    private static Dictionary<string, string?> SwarmSettings()
    {
        return new Dictionary<string, string?> { ["AudioLab:Door:Kind"] = "Swarm", ["AudioLab:Door:ProxySecret"] = Secret };
    }

    private static HttpRequestMessage Proxied(string path, string? user, string secret = Secret)
    {
        HttpRequestMessage request = new(HttpMethod.Get, path);
        request.Headers.Add("X-AudioLab-Secret", secret);
        if (user is not null)
        {
            request.Headers.Add("X-AudioLab-User", user);
        }
        return request;
    }
}
