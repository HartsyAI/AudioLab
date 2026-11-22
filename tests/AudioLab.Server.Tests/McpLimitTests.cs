using System.Net;
using System.Text;
using AudioLab.Core.Commands;
using AudioLab.Core.Model;
using AudioLab.Server.Realtime;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace AudioLab.Server.Tests;

public sealed class McpLimitTests
{
    [Fact]
    public async Task ABodyOverTheConfiguredLimitIsAnswered413AndOneUnderItIsReadEvenWhenBiggerThanOtherRoutesAllow()
    {
        Dictionary<string, string?> settings = new() { ["AudioLab:Mcp:MaxRequestBytes"] = "8192" };
        await using RunningServer server = await McpTestClient.StartAsync(settings);

        using HttpResponseMessage over = await PostAsync(server, new string('x', 9000));
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, over.StatusCode);
        HttpRequestMessage chunked = new(HttpMethod.Post, McpTestClient.Path) { Content = new UnsizedContent(new string('x', 9000)) };
        chunked.Headers.Add("Accept", "application/json, text/event-stream");
        using HttpResponseMessage unsized = await server.Client.SendAsync(chunked);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, unsized.StatusCode);
        using StringContent routeBody = new(new string('x', 70 * 1024), Encoding.UTF8, "application/json");
        using HttpResponseMessage routeOver = await server.Client.PostAsync("/api/projects", routeBody);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, routeOver.StatusCode);

        await using McpClient client = await McpTestClient.ConnectAsync(server);
        CallToolResult large = await client.CallToolAsync("get_project", Padded(4000));
        Assert.Equal(CommandErrorCode.Malformed, Assert.Single(McpTestClient.Reply(large).Errors).Code);
    }

    [Fact]
    public async Task TheDefaultLimitIsOneMebibyteSoItIsAboveWhatTheRoutesTake()
    {
        await using RunningServer server = await McpTestClient.StartAsync();
        await using McpClient client = await McpTestClient.ConnectAsync(server);

        CallToolResult large = await client.CallToolAsync("get_project", Padded(200 * 1024));
        Assert.Equal(CommandErrorCode.Malformed, Assert.Single(McpTestClient.Reply(large).Errors).Code);
        using HttpResponseMessage over = await PostAsync(server, new string('x', (1024 * 1024) + 1));
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, over.StatusCode);
    }

    [Fact]
    public async Task ADocumentOverTheLimitIsRefusedWithLimitInTheToolAndTheResourceAndTheProjectStaysEditable()
    {
        Dictionary<string, string?> settings = new() { ["AudioLab:Mcp:MaxDocumentChars"] = "1024" };
        await using RunningServer server = await McpTestClient.StartAsync(settings);
        string project = await McpTestClient.CreateProjectAsync(server, UserId.Local);
        await using HubClient ui = await HubClient.ConnectAsync(server);
        await ui.JoinAsync(project);
        await using McpClient agent = await McpTestClient.ConnectAsync(server);
        CallToolResult small = await McpTestClient.CallAsync(agent, "get_project", project);
        Assert.NotEqual(true, small.IsError);

        for (int index = 0; index < 20; index++)
        {
            string marker = "{\"project\":\"" + project + "\",\"time\":" + index + ",\"name\":\"A marker\"}";
            Assert.True((await ui.ApplyAsync(project, "add_marker", marker)).Succeeded);
        }

        CallToolResult large = await McpTestClient.CallAsync(agent, "get_project", project);
        Assert.True(large.IsError);
        CommandReply reply = McpTestClient.Reply(large);
        Assert.Equal(CommandErrorCode.Limit, Assert.Single(reply.Errors).Code);
        Assert.Equal(21, reply.Revision);
        Assert.True((await McpTestClient.CallAsync(agent, "get_history", project)).IsError != true);
        McpProtocolException resource = await Assert.ThrowsAsync<McpProtocolException>(
            () => agent.ReadResourceAsync($"audiolab://projects/{project}").AsTask());
        Assert.Equal(McpErrorCode.InvalidRequest, resource.ErrorCode);
    }

    [Fact]
    public async Task ALimitOutsideItsRangeStopsTheServerFromStarting()
    {
        foreach ((string key, string value) in new[]
        {
            ("AudioLab:Mcp:MaxRequestBytes", "10"),
            ("AudioLab:Mcp:MaxRequestBytes", "99999999999"),
            ("AudioLab:Mcp:MaxDocumentChars", "0"),
            ("AudioLab:Mcp:MaxDocumentChars", "99999999"),
        })
        {
            InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(
                () => McpTestClient.StartAsync(new Dictionary<string, string?> { [key] = value }));
            Assert.Contains(key, error.Message, StringComparison.Ordinal);
        }
    }

    private static Dictionary<string, object?> Padded(int length) => new() { ["project"] = "p", ["pad"] = new string('x', length) };

    private static Task<HttpResponseMessage> PostAsync(RunningServer server, string body)
    {
        HttpRequestMessage request = new(HttpMethod.Post, McpTestClient.Path)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("Accept", "application/json, text/event-stream");
        return server.Client.SendAsync(request);
    }

    // A body of unknown length is sent chunked, so only the reading of it can tell that it is too long.
    private sealed class UnsizedContent : HttpContent
    {
        private readonly byte[] _bytes;

        public UnsizedContent(string text)
        {
            _bytes = Encoding.UTF8.GetBytes(text);
            Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
        }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => stream.WriteAsync(_bytes, 0, _bytes.Length);

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }
}
