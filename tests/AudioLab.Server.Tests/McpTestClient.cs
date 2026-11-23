using System.Text.Json;
using AudioLab.Core.Api;
using AudioLab.Core.Model;
using AudioLab.Core.Realtime;
using AudioLab.Server.Services;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace AudioLab.Server.Tests;

/// <summary>A real MCP client of the running server, over Streamable HTTP, and the helpers the MCP tests share.</summary>
public static class McpTestClient
{
    public const string Secret = "0123456789abcdef0123456789abcdef";

    public const string Path = "/api/mcp";

    public static Task<RunningServer> StartAsync(IDictionary<string, string?>? settings = null, Action<IServiceCollection>? configureServices = null)
    {
        return RunningServer.StartAsync(settings, services =>
        {
            services.AddSingleton(TestCommandRegistry.Create());
            configureServices?.Invoke(services);
        });
    }

    public static Dictionary<string, string?> SwarmSettings()
    {
        return new Dictionary<string, string?> { ["AudioLab:Door:Kind"] = "Swarm", ["AudioLab:Door:ProxySecret"] = Secret };
    }

    public static Dictionary<string, string> AsUser(string subject)
    {
        return new Dictionary<string, string> { ["X-AudioLab-Secret"] = Secret, ["X-AudioLab-User"] = subject };
    }

    public static UserId SwarmUser(string subject) => AudioLab.Server.Doors.ProxyDoor.UserFor("swarm", subject);

    public static Task<McpClient> ConnectAsync(RunningServer server, IDictionary<string, string>? headers = null)
    {
        HttpClientTransport transport = new(new HttpClientTransportOptions
        {
            Endpoint = new Uri(server.Client.BaseAddress!, Path),
            TransportMode = HttpTransportMode.StreamableHttp,
            AdditionalHeaders = headers,
        });
        return McpClient.CreateAsync(transport);
    }

    public static async Task<string> CreateProjectAsync(RunningServer server, UserId user, string name = "Song")
    {
        ProjectSummary created = await server.Services.GetRequiredService<ProjectService>().CreateAsync(user, name, CancellationToken.None);
        return created.Id;
    }

    public static ValueTask<CallToolResult> CallAsync(McpClient client, string tool, string? project = null, long? expectedRevision = null)
    {
        Dictionary<string, object?> arguments = [];
        if (project is not null)
        {
            arguments["project"] = project;
        }
        if (expectedRevision is not null)
        {
            arguments["expectedRevision"] = expectedRevision;
        }
        return client.CallToolAsync(tool, arguments);
    }

    /// <summary>Calls undo or redo as an agent has to: with the revision it read.</summary>
    public static ValueTask<CallToolResult> StepAsync(McpClient client, string tool, string project, long expectedRevision)
    {
        return CallAsync(client, tool, project, expectedRevision);
    }

    public static CommandReply Reply(CallToolResult result)
    {
        return Reply(result, HubJsonContext.Default.CommandReply);
    }

    public static T Reply<T>(CallToolResult result, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> info)
    {
        Assert.Null(result.StructuredContent);
        return JsonSerializer.Deserialize(Text(result), info)!;
    }

    /// <summary>The JSON a result carries in its text.</summary>
    public static JsonElement Json(CallToolResult result)
    {
        using JsonDocument document = JsonDocument.Parse(Text(result));
        return document.RootElement.Clone();
    }

    public static string Text(CallToolResult result) => ((TextContentBlock)Assert.Single(result.Content)).Text;
}
