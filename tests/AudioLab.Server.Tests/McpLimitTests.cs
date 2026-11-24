using System.Net;
using System.Text;
using AudioLab.Core.Commands;
using AudioLab.Core.Model;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace AudioLab.Server.Tests;

public sealed class McpLimitTests
{
    [Fact]
    public async Task ABodyOverTheConfiguredLimitIsAnswered413AndOneUnderItIsReadEvenWhenBiggerThanOtherRoutesAllow()
    {
        Dictionary<string, string?> settings = new() { ["AudioLab:Mcp:MaxRequestBytes"] = "262144" };
        await using RunningServer server = await McpTestClient.StartAsync(settings);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, await StatusAsync(server, () => Request(Oversize(300_000), "/api/mcp")));
        Assert.Equal(
            HttpStatusCode.RequestEntityTooLarge,
            await StatusAsync(server, () => Request(new UnsizedContent(Oversize(300_000)), "/api/mcp")));
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, await StatusAsync(server, () => Request(new string('x', 70 * 1024), "/api/projects")));

        await using McpClient client = await McpTestClient.ConnectAsync(server);
        CallToolResult large = await client.CallToolAsync("get_project", Padded(100 * 1024));
        Assert.Equal(CommandErrorCode.Malformed, Assert.Single(McpTestClient.Reply(large).Errors).Code);
    }

    [Fact]
    public async Task TheDefaultLimitIsOneMebibyteSoItIsAboveWhatTheRoutesTake()
    {
        await using RunningServer server = await McpTestClient.StartAsync();
        await using McpClient client = await McpTestClient.ConnectAsync(server);

        CallToolResult large = await client.CallToolAsync("get_project", Padded(200 * 1024));
        Assert.Equal(CommandErrorCode.Malformed, Assert.Single(McpTestClient.Reply(large).Errors).Code);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, await StatusAsync(server, () => Request(Oversize((1024 * 1024) + 1), "/api/mcp")));
    }

    [Fact]
    public async Task AWholeDocumentOverTheLimitIsRefusedAsAResourceWhileTheToolStillReadsItInParts()
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

        // The tool reads the project in parts, so it still answers; only the whole document, as a resource, is over the limit.
        Assert.Equal(21, McpTestClient.Json(await McpTestClient.CallAsync(agent, "get_project", project)).GetProperty("revision").GetInt64());
        Assert.True((await McpTestClient.CallAsync(agent, "get_history", project)).IsError != true);
        McpProtocolException resource = await Assert.ThrowsAsync<McpProtocolException>(
            () => agent.ReadResourceAsync($"audiolab://projects/{project}").AsTask());
        Assert.Equal(McpErrorCode.InvalidParams, resource.ErrorCode);
        Assert.Contains("get_project", resource.Message, StringComparison.Ordinal);
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
            ("AudioLab:Mcp:MaxResultChars", "10"),
            ("AudioLab:Mcp:MaxResultChars", "99999999"),
            ("AudioLab:Mcp:MaxConcurrentCallsPerUser", "0"),
            ("AudioLab:Mcp:MaxConcurrentCallsPerUser", "65"),
        })
        {
            InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(
                () => McpTestClient.StartAsync(new Dictionary<string, string?> { [key] = value }));
            Assert.Contains(key, error.Message, StringComparison.Ordinal);
        }
    }

    // A string that never ends is valid JSON as far as it goes, so only the length of the body can refuse it, however it is read.
    private static string Oversize(int length) => "{\"pad\":\"" + new string('x', length);

    private static Dictionary<string, object?> Padded(int length) => new() { ["project"] = "p", ["pad"] = new string('x', length) };

    private static HttpRequestMessage Request(string body, string path)
    {
        return Request(new StringContent(body, Encoding.UTF8, "application/json"), path);
    }

    private static HttpRequestMessage Request(HttpContent content, string path)
    {
        HttpRequestMessage request = new(HttpMethod.Post, path) { Content = content };
        request.Headers.Add("Accept", "application/json, text/event-stream");
        return request;
    }

    // The server answers a body it will not read without reading it, so an operating system may reset the connection while the client is
    // still sending. That is the refusal arriving early, not a different answer: ask again until the status is seen.
    private static async Task<HttpStatusCode> StatusAsync(RunningServer server, Func<HttpRequestMessage> request)
    {
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                using HttpResponseMessage response = await server.Client.SendAsync(request());
                return response.StatusCode;
            }
            catch (HttpRequestException) when (attempt < 5)
            {
            }
        }
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
