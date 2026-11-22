using System.Text.Json;
using AudioLab.Core.Commands;
using AudioLab.Core.Model;
using AudioLab.Server.Mcp;
using AudioLab.Server.Realtime;
using AudioLab.Server.Store;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace AudioLab.Server.Tests;

public sealed class McpToolTests
{
    private static readonly string[] _builtIns = ["get_history", "get_project", "list_projects", "redo", "undo"];

    [Fact]
    public async Task TheReadAndHistoryToolsAreOfferedWithADescribedSchema()
    {
        await using RunningServer server = await McpTestClient.StartAsync();
        await using McpClient client = await McpTestClient.ConnectAsync(server);

        IList<McpClientTool> tools = await client.ListToolsAsync();
        Assert.Equal(_builtIns, tools.Select(tool => tool.Name).Order(StringComparer.Ordinal).ToArray());
        foreach (McpClientTool tool in tools)
        {
            Assert.False(string.IsNullOrWhiteSpace(tool.Description));
            Assert.Equal("object", tool.JsonSchema.GetProperty("type").GetString());
            if (tool.JsonSchema.TryGetProperty("properties", out JsonElement properties))
            {
                foreach (JsonProperty property in properties.EnumerateObject())
                {
                    Assert.True(property.Value.TryGetProperty("description", out _), $"{tool.Name}.{property.Name} has no description.");
                }
            }
        }
    }

    [Fact]
    public async Task ListGetAndHistoryReadTheUsersProjectAndTheDocumentIsStrictProjectJson()
    {
        await using RunningServer server = await McpTestClient.StartAsync();
        string project = await McpTestClient.CreateProjectAsync(server, UserId.Local);
        await using McpClient client = await McpTestClient.ConnectAsync(server);

        CallToolResult listed = await McpTestClient.CallAsync(client, "list_projects");
        Assert.NotEqual(true, listed.IsError);
        ProjectListing listing = McpTestClient.Reply(listed, McpJsonContext.Default.ProjectListing);
        Assert.Equal([project], listing.Projects.Select(summary => summary.Id).ToArray());

        CallToolResult got = await McpTestClient.CallAsync(client, "get_project", project);
        Assert.NotEqual(true, got.IsError);
        ProjectDocumentResult result = McpTestClient.Reply(got, McpJsonContext.Default.ProjectDocumentResult);
        Assert.Equal(1, result.Revision);
        ProjectDocument document = ProjectJson.Read(result.Document.GetRawText());
        Assert.Equal((project, "Song", UserId.Local), (document.Id.Value, document.Name, document.Owner));
        Assert.Equal(McpTestClient.Text(got), got.StructuredContent!.Value.GetRawText());

        CallToolResult history = await McpTestClient.CallAsync(client, "get_history", project);
        Assert.Equal(new HistoryState(1, 0, 0, null, null), McpTestClient.Reply(history, McpJsonContext.Default.HistoryState));
    }

    [Fact]
    public async Task AnAgentsUndoAndRedoReachEveryClientOfTheProjectAndTheHistoryShowsTheSteps()
    {
        await using RunningServer server = await McpTestClient.StartAsync();
        string project = await McpTestClient.CreateProjectAsync(server, UserId.Local);
        await using HubClient ui = await HubClient.ConnectAsync(server);
        await ui.JoinAsync(project);
        await using McpClient agent = await McpTestClient.ConnectAsync(server);

        Assert.True((await ui.ApplyAsync(project, "set_bpm", "{\"project\":\"" + project + "\",\"bpm\":90}")).Succeeded);
        Assert.Equal(90, ProjectJson.Read((await ui.NextChangeAsync()).Document).Bpm);
        CallToolResult history = await McpTestClient.CallAsync(agent, "get_history", project);
        Assert.Equal(new HistoryState(2, 1, 0, "set_bpm", null), McpTestClient.Reply(history, McpJsonContext.Default.HistoryState));

        CommandReply undone = McpTestClient.Reply(await McpTestClient.CallAsync(agent, "undo", project));
        Assert.True(undone.Succeeded && undone.Changed);
        ProjectChange undoChange = await ui.NextChangeAsync();
        Assert.Equal((project, 3L, "undo"), (undoChange.Project, undoChange.Revision, undoChange.Reason));
        Assert.Equal(120, ProjectJson.Read(undoChange.Document).Bpm);

        CommandReply redone = McpTestClient.Reply(await McpTestClient.CallAsync(agent, "redo", project));
        Assert.True(redone.Succeeded && redone.Changed);
        ProjectChange redoChange = await ui.NextChangeAsync();
        Assert.Equal((4L, "redo", 90d), (redoChange.Revision, redoChange.Reason, ProjectJson.Read(redoChange.Document).Bpm));
    }

    [Fact]
    public async Task UndoAndRedoWithNothingToDoAreRefusedAsRejectedAndNobodyHearsIt()
    {
        await using RunningServer server = await McpTestClient.StartAsync();
        string project = await McpTestClient.CreateProjectAsync(server, UserId.Local);
        await using HubClient ui = await HubClient.ConnectAsync(server);
        await ui.JoinAsync(project);
        await using McpClient agent = await McpTestClient.ConnectAsync(server);

        foreach (string tool in new[] { "undo", "redo" })
        {
            CallToolResult result = await McpTestClient.CallAsync(agent, tool, project);
            Assert.True(result.IsError);
            CommandReply reply = McpTestClient.Reply(result);
            Assert.False(reply.Succeeded);
            Assert.Equal(CommandErrorCode.Rejected, Assert.Single(reply.Errors).Code);
        }
        Assert.True(await ui.HeardNothingAsync());
    }

    [Fact]
    public async Task BadArgumentsAreAMalformedRefusalAndAMissingProjectIsNotFound()
    {
        await using RunningServer server = await McpTestClient.StartAsync();
        await using McpClient client = await McpTestClient.ConnectAsync(server);

        Dictionary<string, object?>[] malformed =
        [
            [],
            new() { ["project"] = "Not An Id" },
            new() { ["project"] = 7 },
            new() { ["project"] = "p1", ["owner"] = "swarm-bob" },
        ];
        foreach (Dictionary<string, object?> arguments in malformed)
        {
            CallToolResult result = await client.CallToolAsync("get_project", arguments);
            Assert.True(result.IsError);
            Assert.Equal(CommandErrorCode.Malformed, Assert.Single(McpTestClient.Reply(result).Errors).Code);
        }
        CallToolResult extra = await client.CallToolAsync("list_projects", new Dictionary<string, object?> { ["all"] = true });
        Assert.Equal(CommandErrorCode.Malformed, Assert.Single(McpTestClient.Reply(extra).Errors).Code);

        foreach (string tool in new[] { "get_project", "get_history", "undo", "redo" })
        {
            CallToolResult missing = await McpTestClient.CallAsync(client, tool, "nosuchproject");
            Assert.True(missing.IsError);
            Assert.Equal(CommandErrorCode.NotFound, Assert.Single(McpTestClient.Reply(missing).Errors).Code);
        }
    }

    [Fact]
    public async Task AToolTheServerDoesNotHaveIsAProtocolErrorNotAResult()
    {
        await using RunningServer server = await McpTestClient.StartAsync();
        await using McpClient client = await McpTestClient.ConnectAsync(server);

        McpProtocolException error = await Assert.ThrowsAsync<McpProtocolException>(
            () => client.CallToolAsync("no_such_tool", new Dictionary<string, object?>()).AsTask());
        Assert.Equal(McpErrorCode.InvalidParams, error.ErrorCode);
    }
}
