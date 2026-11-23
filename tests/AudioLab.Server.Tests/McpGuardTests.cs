using System.Net;
using System.Text;
using AudioLab.Core.Model;
using AudioLab.Server.Doors;
using AudioLab.Server.Mcp;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Client;

namespace AudioLab.Server.Tests;

public sealed class McpGuardTests
{
    private const string Initialize = "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\",\"params\":{\"protocolVersion\":\"2025-06-18\","
        + "\"capabilities\":{},\"clientInfo\":{\"name\":\"test\",\"version\":\"1\"}}}";

    [Fact]
    public async Task OnEveryDoorAPostThatIsNotJsonIsAnswered415BeforeAnythingIsRead()
    {
        foreach (Dictionary<string, string?> settings in new[] { new Dictionary<string, string?>(), McpTestClient.SwarmSettings() })
        {
            await using RunningServer server = await McpTestClient.StartAsync(settings);
            Dictionary<string, string> door = settings.Count == 0 ? [] : McpTestClient.AsUser("alice");

            foreach (string type in new[] { "text/plain", "application/x-www-form-urlencoded", "multipart/form-data", "application/jsonx" })
            {
                using HttpResponseMessage response = await server.Client.SendAsync(Post(Initialize, type, door));
                Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
            }
            using HttpRequestMessage none = Post(Initialize, "application/json", door);
            none.Content!.Headers.ContentType = null;
            using HttpResponseMessage missing = await server.Client.SendAsync(none);
            Assert.Equal(HttpStatusCode.UnsupportedMediaType, missing.StatusCode);

            using HttpResponseMessage json = await server.Client.SendAsync(Post(Initialize, "application/json; charset=utf-8", door));
            Assert.Equal(HttpStatusCode.OK, json.StatusCode);
        }
    }

    [Fact]
    public async Task OnAProxyDoorARequestARealBrowserMarksAsFromAnotherOriginOrSiteIsRefusedEvenWithTheProxyHeaders()
    {
        await using RunningServer server = await McpTestClient.StartAsync(McpTestClient.SwarmSettings());

        foreach ((string header, string value) in new[]
        {
            ("Origin", "https://evil.example"),
            ("Origin", "null"),
            ("Sec-Fetch-Site", "cross-site"),
            ("Sec-Fetch-Site", "same-site"),
        })
        {
            Dictionary<string, string> headers = McpTestClient.AsUser("alice");
            headers[header] = value;
            using HttpResponseMessage response = await server.Client.SendAsync(Post(Initialize, "application/json", headers));
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            await Assert.ThrowsAnyAsync<Exception>(() => McpTestClient.ConnectAsync(server, headers));
        }
        Dictionary<string, string> sameOrigin = McpTestClient.AsUser("alice");
        sameOrigin["Sec-Fetch-Site"] = "same-origin";
        using HttpResponseMessage allowed = await server.Client.SendAsync(Post(Initialize, "application/json", sameOrigin));
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
    }

    [Fact]
    public async Task ARequestWithNoBrowserHeadersIsAnAgentAndIsAdmittedOnEveryDoor()
    {
        foreach (Dictionary<string, string?> settings in new[] { new Dictionary<string, string?>(), McpTestClient.SwarmSettings() })
        {
            await using RunningServer server = await McpTestClient.StartAsync(settings);
            await using McpClient client = await McpTestClient.ConnectAsync(server, settings.Count == 0 ? null : McpTestClient.AsUser("alice"));
            Assert.Contains("list_projects", (await client.ListToolsAsync()).Select(tool => tool.Name));
        }
    }

    [Fact]
    public async Task AUserOverTheConcurrencyLimitIsAnswered429WithRetryAfterAndAnotherUserAndALaterCallAreNot()
    {
        McpCallLimiter limiter = new(maxPerUser: 1);
        using SemaphoreSlim entered = new(0);
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        RequestDelegate guarded = McpRequestGuard.Wrap(
            async _ =>
            {
                entered.Release();
                await release.Task;
            },
            new McpOptions(),
            limiter);

        HttpContext first = Context("alice");
        Task running = guarded(first);
        await entered.WaitAsync(TimeSpan.FromSeconds(10));

        HttpContext second = Context("alice");
        await guarded(second);
        Assert.Equal(StatusCodes.Status429TooManyRequests, second.Response.StatusCode);
        Assert.Equal("1", second.Response.Headers.RetryAfter.ToString());

        Task other = guarded(Context("bob"));
        await entered.WaitAsync(TimeSpan.FromSeconds(10));
        release.SetResult();
        await Task.WhenAll(running, other);

        HttpContext later = Context("alice");
        await guarded(later);
        Assert.Equal(StatusCodes.Status200OK, later.Response.StatusCode);
    }

    [Fact]
    public async Task ASlotIsGivenBackWhenTheCallFailsAndTwiceDisposingTakesOnlyOne()
    {
        McpCallLimiter limiter = new(maxPerUser: 1);
        UserId alice = UserId.From("alice");

        IDisposable? slot = limiter.TryEnter(alice);
        Assert.NotNull(slot);
        Assert.Null(limiter.TryEnter(alice));
        slot.Dispose();
        slot.Dispose();
        IDisposable? again = limiter.TryEnter(alice);
        Assert.NotNull(again);
        Assert.Null(limiter.TryEnter(alice));
        again.Dispose();

        RequestDelegate failing = McpRequestGuard.Wrap(_ => throw new InvalidOperationException("boom"), new McpOptions(), limiter);
        await Assert.ThrowsAsync<InvalidOperationException>(() => failing(Context("alice")));
        Assert.NotNull(limiter.TryEnter(alice));
    }

    private static HttpRequestMessage Post(string body, string contentType, Dictionary<string, string> headers)
    {
        HttpRequestMessage request = new(HttpMethod.Post, McpTestClient.Path)
        {
            Content = new StringContent(body, Encoding.UTF8, contentType.Split(';')[0]),
        };
        request.Content.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse(contentType);
        request.Headers.Add("Accept", "application/json, text/event-stream");
        foreach (KeyValuePair<string, string> header in headers)
        {
            request.Headers.Add(header.Key, header.Value);
        }
        return request;
    }

    private static DefaultHttpContext Context(string user)
    {
        DefaultHttpContext context = new();
        context.RequestServices = new ServiceCollection().AddLogging().BuildServiceProvider();
        context.Request.Method = HttpMethods.Post;
        context.Request.ContentType = "application/json";
        context.Request.Host = new HostString("localhost");
        context.Features.Set<IHttpMaxRequestBodySizeFeature>(new BodySizeFeature());
        context.SetUser(UserId.From(user));
        return context;
    }

    private sealed class BodySizeFeature : IHttpMaxRequestBodySizeFeature
    {
        public bool IsReadOnly => false;

        public long? MaxRequestBodySize { get; set; }
    }
}
