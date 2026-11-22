using System.Net;
using System.Net.WebSockets;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

namespace AudioLab.Server.Tests;

public sealed class HubDoorTests
{
    private const string Secret = "0123456789abcdef0123456789abcdef";

    [Fact]
    public async Task APageFromAnotherOriginCannotConnectToTheHubOnTheDesktopDoor()
    {
        await using RunningServer server = await Start();
        string own = server.Client.BaseAddress!.GetLeftPart(UriPartial.Authority);

        await using (await HubClient.ConnectAsync(server))
        {
        }
        await using (await HubClient.ConnectAsync(server, options => options.Headers["Origin"] = own))
        {
        }
        foreach (string origin in new[] { "http://evil.example", "null", "http://127.0.0.1:1" })
        {
            await using HubClient refused = HubClient.Build(server, options => options.Headers["Origin"] = origin);
            await Assert.ThrowsAnyAsync<Exception>(() => refused.Connection.StartAsync());
        }
        await using HubClient crossSite = HubClient.Build(server, options => options.Headers["Sec-Fetch-Site"] = "cross-site");
        await Assert.ThrowsAnyAsync<Exception>(() => crossSite.Connection.StartAsync());
    }

    [Fact]
    public async Task TheNegotiateRequestIsRefusedForAForeignHostOriginOrFetchSite()
    {
        await using RunningServer server = await Start();
        string negotiate = server.HubPath + "/negotiate?negotiateVersion=1";
        Assert.Equal(HttpStatusCode.OK, (await Post(server, negotiate)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(server, negotiate, host: "rebind.evil.example")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(server, negotiate, origin: "http://evil.example")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(server, negotiate, site: "cross-site")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(server, negotiate, site: "same-site")).StatusCode);
    }

    [Fact]
    public async Task ABrowserWebSocketFromAnotherOriginIsRefusedAtTheUpgrade()
    {
        await using RunningServer server = await Start();
        UriBuilder address = new(new Uri(server.Client.BaseAddress!, server.HubPath)) { Scheme = "ws" };
        using ClientWebSocket socket = new();
        socket.Options.SetRequestHeader("Origin", "http://evil.example");
        WebSocketException error = await Assert.ThrowsAsync<WebSocketException>(() => socket.ConnectAsync(address.Uri, CancellationToken.None));
        Assert.Contains("403", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AProxyDoorHubNeedsTheProxySecretAndAUser()
    {
        await using RunningServer server = await Start(new Dictionary<string, string?> { ["AudioLab:Door:Kind"] = "Swarm", ["AudioLab:Door:ProxySecret"] = Secret });
        await using HubClient noSecret = HubClient.Build(server, options => options.Headers["X-AudioLab-User"] = "alice");
        await Assert.ThrowsAnyAsync<Exception>(() => noSecret.Connection.StartAsync());
        await using HubClient wrongSecret = HubClient.Build(server, options =>
        {
            options.Headers["X-AudioLab-Secret"] = "0123456789abcdef0123456789abcdee";
            options.Headers["X-AudioLab-User"] = "alice";
        });
        await Assert.ThrowsAnyAsync<Exception>(() => wrongSecret.Connection.StartAsync());
        await using HubClient noUser = HubClient.Build(server, options => options.Headers["X-AudioLab-Secret"] = Secret);
        await Assert.ThrowsAnyAsync<Exception>(() => noUser.Connection.StartAsync());
        await using HubClient admitted = await HubClient.ConnectAsync(server, options =>
        {
            options.Headers["X-AudioLab-Secret"] = Secret;
            options.Headers["X-AudioLab-User"] = "alice";
        });
        Assert.Equal(HubConnectionState.Connected, admitted.Connection.State);
    }

    [Fact]
    public async Task OnlyWebSocketsAreOffered()
    {
        await using RunningServer server = await Start();
        using HttpResponseMessage response = await Post(server, server.HubPath + "/negotiate?negotiateVersion=1");
        string body = await response.Content.ReadAsStringAsync();
        Assert.Contains("WebSockets", body, StringComparison.Ordinal);
        Assert.DoesNotContain("LongPolling", body, StringComparison.Ordinal);
        Assert.DoesNotContain("ServerSentEvents", body, StringComparison.Ordinal);
    }

    private static Task<RunningServer> Start(IDictionary<string, string?>? settings = null)
    {
        return RunningServer.StartAsync(settings, services => services.AddSingleton(TestCommandRegistry.Create()));
    }

    private static async Task<HttpResponseMessage> Post(RunningServer server, string path, string? host = null, string? origin = null, string? site = null)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, path);
        if (host is not null)
        {
            request.Headers.Host = host;
        }
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
