using System.Net;
using System.Text;
using AudioLab.Core.Commands;
using AudioLab.Core.Model;
using AudioLab.Server.Doors;
using AudioLab.Server.Mcp;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace AudioLab.Server.Tests;

public sealed class McpIdentityTests
{
    private const string Initialize = "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\",\"params\":{\"protocolVersion\":\"2025-06-18\","
        + "\"capabilities\":{},\"clientInfo\":{\"name\":\"test\",\"version\":\"1\"}}}";

    [Fact]
    public async Task OnlyTheHealthRouteIsOutsideTheDoor()
    {
        await using RunningServer server = await McpTestClient.StartAsync();

        RouteEndpoint[] endpoints = [.. server.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()];
        Assert.Contains(endpoints, endpoint => endpoint.RoutePattern.RawText!.TrimEnd('/') == "/api/mcp");
        string[] outside =
        [
            .. endpoints.Where(endpoint => endpoint.Metadata.GetMetadata<DoorRequired>() is null
                && endpoint.Metadata.GetMetadata<AudioLab.Server.Routes.UiEndpoint>() is null).Select(endpoint => endpoint.RoutePattern.RawText!),
        ];
        Assert.Equal(["/health"], outside);
    }

    [Fact]
    public async Task AnAgentActsAsTheUserTheDoorResolvedAndSeesOnlyThatUsersProjects()
    {
        await using RunningServer server = await McpTestClient.StartAsync(McpTestClient.SwarmSettings());
        string alices = await McpTestClient.CreateProjectAsync(server, McpTestClient.SwarmUser("alice"), "Alice's song");
        string bobs = await McpTestClient.CreateProjectAsync(server, McpTestClient.SwarmUser("bob"), "Bob's song");
        await using McpClient alice = await McpTestClient.ConnectAsync(server, McpTestClient.AsUser("alice"));
        await using McpClient bob = await McpTestClient.ConnectAsync(server, McpTestClient.AsUser("bob"));

        Assert.Equal([alices], Listed(await McpTestClient.CallAsync(alice, "list_projects")));
        Assert.Equal([bobs], Listed(await McpTestClient.CallAsync(bob, "list_projects")));

        CallToolResult missing = await McpTestClient.CallAsync(bob, "get_project", "nosuchproject");
        foreach (string tool in new[] { "get_project", "get_history", "undo", "redo" })
        {
            CallToolResult refused = await McpTestClient.CallAsync(bob, tool, alices, tool is "undo" or "redo" ? 1 : null);
            Assert.True(refused.IsError);
            Assert.Equal(CommandErrorCode.NotFound, Assert.Single(McpTestClient.Reply(refused).Errors).Code);
            Assert.Equal(Assert.Single(McpTestClient.Reply(missing).Errors).Message, Assert.Single(McpTestClient.Reply(refused).Errors).Message);
        }
        CallToolResult own = await McpTestClient.CallAsync(alice, "get_project", alices);
        Assert.Equal(1, McpTestClient.Json(own).GetProperty("revision").GetInt64());
    }

    [Fact]
    public async Task TheDoorAnswersBeforeAnyMcpMessageIsRead()
    {
        await using RunningServer server = await McpTestClient.StartAsync(McpTestClient.SwarmSettings());

        Dictionary<string, string>[] refused =
        [
            [],
            new() { ["X-AudioLab-User"] = "alice" },
            new() { ["X-AudioLab-Secret"] = "0123456789abcdef0123456789abcdee", ["X-AudioLab-User"] = "alice" },
            new() { ["X-AudioLab-Secret"] = McpTestClient.Secret },
        ];
        foreach (Dictionary<string, string> headers in refused)
        {
            await Assert.ThrowsAnyAsync<Exception>(() => McpTestClient.ConnectAsync(server, headers));
            using HttpResponseMessage response = await PostAsync(server, Initialize, headers);
            Assert.True(response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized, response.StatusCode.ToString());
        }
        await using McpClient admitted = await McpTestClient.ConnectAsync(server, McpTestClient.AsUser("alice"));
        Assert.Contains(await admitted.ListToolsAsync(), tool => tool.Name == "list_projects");
    }

    [Fact]
    public async Task OnTheDesktopDoorAPageFromAnotherOriginCannotReachTheAgentEndpoint()
    {
        await using RunningServer server = await McpTestClient.StartAsync();

        using HttpResponseMessage own = await PostAsync(server, Initialize, []);
        Assert.Equal(HttpStatusCode.OK, own.StatusCode);
        foreach (string header in new[] { "Origin", "Sec-Fetch-Site" })
        {
            Dictionary<string, string> headers = new() { [header] = header == "Origin" ? "http://evil.example" : "cross-site" };
            using HttpResponseMessage response = await PostAsync(server, Initialize, headers);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            await Assert.ThrowsAnyAsync<Exception>(() => McpTestClient.ConnectAsync(server, headers));
        }
        using HttpRequestMessage rebound = Request(server, HttpMethod.Post, Initialize, []);
        rebound.Headers.Host = "rebind.evil.example";
        using HttpResponseMessage rebinding = await server.Client.SendAsync(rebound);
        Assert.Equal(HttpStatusCode.Forbidden, rebinding.StatusCode);
    }

    [Fact]
    public async Task TheEndpointKeepsNoSessionSoAnotherUserCannotReplayOne()
    {
        await using RunningServer server = await McpTestClient.StartAsync(McpTestClient.SwarmSettings());
        string alices = await McpTestClient.CreateProjectAsync(server, McpTestClient.SwarmUser("alice"), "Alice's song");
        string bobsProject = await McpTestClient.CreateProjectAsync(server, McpTestClient.SwarmUser("bob"), "Bob's song");

        using HttpResponseMessage initialized = await PostAsync(server, Initialize, McpTestClient.AsUser("alice"));
        Assert.Equal(HttpStatusCode.OK, initialized.StatusCode);
        Assert.False(initialized.Headers.Contains("Mcp-Session-Id"));
        const string ListProjects = "{\"jsonrpc\":\"2.0\",\"id\":2,\"method\":\"tools/call\","
            + "\"params\":{\"name\":\"list_projects\",\"arguments\":{}}}";
        // A session id is not a credential here: a request that carries one is refused, and one without it is Bob's alone.
        using HttpResponseMessage replay = await PostAsync(server, ListProjects, McpTestClient.AsUser("bob"), session: "anything");
        Assert.Equal(HttpStatusCode.BadRequest, replay.StatusCode);
        Assert.Contains("stateless", await replay.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        using HttpResponseMessage bobs = await PostAsync(server, ListProjects, McpTestClient.AsUser("bob"));
        string answer = await bobs.Content.ReadAsStringAsync();
        Assert.True(bobs.StatusCode == HttpStatusCode.OK, answer);
        Assert.False(bobs.Headers.Contains("Mcp-Session-Id"));
        Assert.Contains(bobsProject, answer, StringComparison.Ordinal);
        Assert.DoesNotContain(alices, answer, StringComparison.Ordinal);
        using HttpResponseMessage get = await server.Client.SendAsync(Request(server, HttpMethod.Get, null, McpTestClient.AsUser("alice")));
        Assert.Equal(HttpStatusCode.MethodNotAllowed, get.StatusCode);
        using HttpResponseMessage delete = await server.Client.SendAsync(Request(server, HttpMethod.Delete, null, McpTestClient.AsUser("alice")));
        Assert.Equal(HttpStatusCode.MethodNotAllowed, delete.StatusCode);
    }

    [Fact]
    public async Task AnArgumentThatNamesAClientOrAGestureIsRefusedNotObeyed()
    {
        await using RunningServer server = await McpTestClient.StartAsync();
        string project = await McpTestClient.CreateProjectAsync(server, UserId.Local);
        await using McpClient agent = await McpTestClient.ConnectAsync(server);

        // An argument that names a client or a gesture is not a member of any tool: it is refused, not obeyed.
        Dictionary<string, object?> arguments = new() { ["project"] = project, ["expectedRevision"] = 1, ["client"] = "hub-1", ["gesture"] = "g1" };
        CallToolResult named = await agent.CallToolAsync("undo", arguments);
        Assert.Equal(CommandErrorCode.Malformed, Assert.Single(McpTestClient.Reply(named).Errors).Code);
    }

    private static string[] Listed(CallToolResult result)
    {
        return [.. McpTestClient.Reply(result, McpJsonContext.Default.ProjectListing).Projects.Select(summary => summary.Id)];
    }

    private static Task<HttpResponseMessage> PostAsync(RunningServer server, string body, Dictionary<string, string> headers, string? session = null)
    {
        HttpRequestMessage request = Request(server, HttpMethod.Post, body, headers);
        if (session is not null)
        {
            request.Headers.Add("Mcp-Session-Id", session);
        }
        return server.Client.SendAsync(request);
    }

    private static HttpRequestMessage Request(RunningServer server, HttpMethod method, string? body, Dictionary<string, string> headers)
    {
        HttpRequestMessage request = new(method, McpTestClient.Path);
        request.Headers.Add("Accept", "application/json, text/event-stream");
        foreach (KeyValuePair<string, string> header in headers)
        {
            request.Headers.Add(header.Key, header.Value);
        }
        if (body is not null)
        {
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        }
        return request;
    }
}
