using System.Text.Json;
using AudioLab.Core.Commands;
using AudioLab.Core.Model;
using AudioLab.Server.Mcp;
using AudioLab.Server.Realtime;
using AudioLab.Server.Store;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace AudioLab.Server.Tests;

public sealed class McpToolTests
{
    private static readonly string[] _builtIns = ["get_history", "get_project", "list_projects", "redo", "undo"];

    [Fact]
    public async Task TheReadAndHistoryToolsAreOfferedBesideTheCommandsWithADescribedSchema()
    {
        await using RunningServer server = await McpTestClient.StartAsync();
        await using McpClient client = await McpTestClient.ConnectAsync(server);

        IList<McpClientTool> tools = await client.ListToolsAsync();
        string[] commands = [.. server.Services.GetRequiredService<CommandRegistry>().Commands.Select(command => command.Name)];
        string[] offered = [.. tools.Select(tool => tool.Name).Order(StringComparer.Ordinal)];
        string[] expected = [.. _builtIns, .. commands];
        Assert.Equal(expected.Order(StringComparer.Ordinal).ToArray(), offered);
        foreach (McpClientTool tool in tools.Where(candidate => _builtIns.Contains(candidate.Name)))
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
    public async Task ListGetAndHistoryReadTheUsersProject()
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
        JsonElement summary = McpTestClient.Json(got);
        Assert.Equal(1, summary.GetProperty("revision").GetInt64());
        Assert.Equal("Song", summary.GetProperty("name").GetString());
        Assert.Equal(0, summary.GetProperty("trackCount").GetInt32());
        Assert.Null(got.StructuredContent);

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

        CommandReply undone = McpTestClient.Reply(await McpTestClient.StepAsync(agent, "undo", project, 2));
        Assert.True(undone.Succeeded && undone.Changed);
        ProjectChange undoChange = await ui.NextChangeAsync();
        Assert.Equal((project, 3L, "undo"), (undoChange.Project, undoChange.Revision, undoChange.Reason));
        Assert.Equal(120, ProjectJson.Read(undoChange.Document).Bpm);

        CommandReply redone = McpTestClient.Reply(await McpTestClient.StepAsync(agent, "redo", project, 3));
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
            CallToolResult result = await McpTestClient.StepAsync(agent, tool, project, 1);
            Assert.True(result.IsError);
            CommandReply reply = McpTestClient.Reply(result);
            Assert.False(reply.Succeeded);
            Assert.Equal(CommandErrorCode.Rejected, Assert.Single(reply.Errors).Code);
        }
        Assert.True(await ui.HeardNothingAsync());
    }

    [Fact]
    public async Task UndoAndRedoNeedTheRevisionTheAgentReadAndAnUndoAfterAnotherUsersEditIsRefusedAndChangesNothing()
    {
        await using RunningServer server = await McpTestClient.StartAsync();
        string project = await McpTestClient.CreateProjectAsync(server, UserId.Local);
        await using HubClient ui = await HubClient.ConnectAsync(server);
        await ui.JoinAsync(project);
        await using McpClient agent = await McpTestClient.ConnectAsync(server);
        Assert.True((await ui.ApplyAsync(project, "set_bpm", "{\"project\":\"" + project + "\",\"bpm\":90}")).Succeeded);
        _ = await ui.NextChangeAsync();
        long read = McpTestClient.Reply(await McpTestClient.CallAsync(agent, "get_history", project), McpJsonContext.Default.HistoryState).Revision;

        // A person edits between the agent's read and its undo: the undo would take back their edit, not the one the agent saw.
        Assert.True((await ui.ApplyAsync(project, "rename_project", "{\"project\":\"" + project + "\",\"name\":\"Mine\"}")).Succeeded);
        ProjectChange theirs = await ui.NextChangeAsync();

        CallToolResult refused = await McpTestClient.StepAsync(agent, "undo", project, read);
        Assert.True(refused.IsError);
        CommandReply reply = McpTestClient.Reply(refused);
        Assert.Equal(CommandErrorCode.Rejected, Assert.Single(reply.Errors).Code);
        Assert.Equal((theirs.Revision, false), (reply.Revision, reply.Changed));
        Assert.True(await ui.HeardNothingAsync());
        Dictionary<string, object?> settings = new() { ["project"] = project, ["section"] = "settings" };
        Assert.Equal("Mine", McpTestClient.Json(await agent.CallToolAsync("get_project", settings)).GetProperty("name").GetString());
        HistoryState after = McpTestClient.Reply(await McpTestClient.CallAsync(agent, "get_history", project), McpJsonContext.Default.HistoryState);
        Assert.Equal(theirs.Revision, after.Revision);

        // Read again, and the same call goes through.
        Assert.True(McpTestClient.Reply(await McpTestClient.StepAsync(agent, "undo", project, theirs.Revision)).Succeeded);
        Assert.Equal("Song", ProjectJson.Read((await ui.NextChangeAsync()).Document).Name);

        foreach (string tool in new[] { "undo", "redo" })
        {
            CallToolResult missing = await McpTestClient.CallAsync(agent, tool, project);
            Assert.Equal(CommandErrorCode.Malformed, Assert.Single(McpTestClient.Reply(missing).Errors).Code);
        }
    }

    [Fact]
    public async Task AnUndoWhileAPersonsGestureIsOpenClosesItAndTakesItBackAsTheHubsUndoDoesAndAMoveInBetweenIsRefused()
    {
        await using RunningServer server = await McpTestClient.StartAsync();
        string project = await McpTestClient.CreateProjectAsync(server, UserId.Local);
        await using HubClient ui = await HubClient.ConnectAsync(server);
        await ui.JoinAsync(project);
        await using McpClient agent = await McpTestClient.ConnectAsync(server);
        string bpm(int value) => "{\"project\":\"" + project + "\",\"bpm\":" + value + "}";

        // A person is dragging: the first move of gesture g1 is applied and the gesture is not ended.
        Assert.True((await ui.ApplyAsync(project, "set_bpm", bpm(90), "g1")).Succeeded);
        _ = await ui.NextChangeAsync();
        HistoryState open = McpTestClient.Reply(await McpTestClient.CallAsync(agent, "get_history", project), McpJsonContext.Default.HistoryState);

        // History shows nothing to undo, as an open gesture is not counted, though the revision has moved.
        Assert.Equal(new HistoryState(2, 0, 0, null, null), open);

        // The person moves again before the agent's undo: the revision no longer matches, so the agent is refused and nothing changes.
        Assert.True((await ui.ApplyAsync(project, "set_bpm", bpm(95), "g1")).Succeeded);
        _ = await ui.NextChangeAsync();
        CallToolResult stale = await McpTestClient.StepAsync(agent, "undo", project, open.Revision);
        Assert.Equal(CommandErrorCode.Rejected, Assert.Single(McpTestClient.Reply(stale).Errors).Code);
        Assert.True(await ui.HeardNothingAsync());

        // The person pauses and the agent reads the revision it sees now: the undo goes through and takes the drag back, as the hub's would.
        HistoryState paused = McpTestClient.Reply(await McpTestClient.CallAsync(agent, "get_history", project), McpJsonContext.Default.HistoryState);
        CommandReply undone = McpTestClient.Reply(await McpTestClient.StepAsync(agent, "undo", project, paused.Revision));
        Assert.True(undone.Succeeded && undone.Changed);
        ProjectChange change = await ui.NextChangeAsync();
        Assert.Equal((paused.Revision + 1, "undo", 120d), (change.Revision, change.Reason, ProjectJson.Read(change.Document).Bpm));
        HistoryState after = McpTestClient.Reply(await McpTestClient.CallAsync(agent, "get_history", project), McpJsonContext.Default.HistoryState);
        Assert.Equal((0, 1, "set_bpm"), (after.UndoCount, after.RedoCount, after.RedoLabel));
        await ui.EndGestureAsync(project, "g1");
        Assert.True(await ui.HeardNothingAsync());
    }

    [Fact]
    public async Task TheUndoAndRedoDescriptionsWarnThatTheHistoryIsSharedAndSayWhatEveryCommandDoesToIt()
    {
        await using RunningServer server = await McpTestClient.StartAsync();
        await using McpClient client = await McpTestClient.ConnectAsync(server);

        foreach (McpClientTool tool in (await client.ListToolsAsync()).Where(tool => tool.Name is "undo" or "redo"))
        {
            Assert.Contains("shared", tool.Description, StringComparison.Ordinal);
            Assert.Contains("expectedRevision", tool.Description, StringComparison.Ordinal);
            Assert.Contains("one undo step", tool.Description, StringComparison.Ordinal);
            Assert.Contains("clears the redo", tool.Description, StringComparison.Ordinal);
            Assert.Contains("expectedRevision", tool.JsonSchema.GetProperty("required").ToString(), StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task AResultOverTheLimitIsALimitRefusalThatNamesTheWayToReadLessAndAShorterReadWorks()
    {
        Dictionary<string, string?> settings = new() { ["AudioLab:Mcp:MaxResultChars"] = "1024" };
        await using RunningServer server = await McpTestClient.StartAsync(settings);
        string project = await McpTestClient.CreateProjectAsync(server, UserId.Local);
        await using HubClient ui = await HubClient.ConnectAsync(server);
        await ui.JoinAsync(project);
        await using McpClient agent = await McpTestClient.ConnectAsync(server);
        for (int index = 0; index < 30; index++)
        {
            string marker = "{\"project\":\"" + project + "\",\"time\":" + index + ",\"name\":\"A marker\"}";
            Assert.True((await ui.ApplyAsync(project, "add_marker", marker)).Succeeded);
        }

        Dictionary<string, object?> all = new() { ["project"] = project, ["section"] = "markers" };
        CallToolResult large = await agent.CallToolAsync("get_project", all);
        Assert.True(large.IsError);
        CommandReply refusal = McpTestClient.Reply(large);
        HubCommandError error = Assert.Single(refusal.Errors);
        Assert.Equal(CommandErrorCode.Limit, error.Code);
        Assert.Contains("count", error.Message, StringComparison.Ordinal);
        Assert.Equal(31, refusal.Revision);

        all["count"] = 3;
        all["offset"] = 10;
        JsonElement page = McpTestClient.Json(await agent.CallToolAsync("get_project", all));
        Assert.Equal(30, page.GetProperty("total").GetInt32());
        Assert.Equal(10, page.GetProperty("offset").GetInt32());
        Assert.Equal(3, page.GetProperty("items").GetArrayLength());
        Assert.Equal(10, page.GetProperty("items")[0].GetProperty("time").GetDouble());
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
            CallToolResult missing = await McpTestClient.CallAsync(client, tool, "nosuchproject", tool is "undo" or "redo" ? 1 : null);
            Assert.True(missing.IsError);
            Assert.Equal(CommandErrorCode.NotFound, Assert.Single(McpTestClient.Reply(missing).Errors).Code);
        }
    }

    [Fact]
    public async Task EveryToolNameIsAValidMcpNameAndNoSchemaHasARefPointer()
    {
        await using RunningServer server = await McpTestClient.StartAsync();
        await using McpClient client = await McpTestClient.ConnectAsync(server);

        foreach (McpClientTool tool in await client.ListToolsAsync())
        {
            Assert.Matches("^[a-zA-Z0-9_-]{1,64}$", tool.Name);
            Assert.DoesNotContain("\"$ref\"", tool.JsonSchema.GetRawText(), StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task AnArgumentNamedTwiceIsReadAsItsLastValueBecauseTheSdkHandsOverADictionary()
    {
        await using RunningServer server = await McpTestClient.StartAsync();
        string project = await McpTestClient.CreateProjectAsync(server, UserId.Local);
        string call = "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"tools/call\",\"params\":{\"name\":\"get_history\","
            + "\"arguments\":{\"project\":\"nosuchproject\",\"project\":\"" + project + "\"}}}";
        using HttpRequestMessage request = new(HttpMethod.Post, McpTestClient.Path)
        {
            Content = new StringContent(call, System.Text.Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("Accept", "application/json, text/event-stream");

        using HttpResponseMessage response = await server.Client.SendAsync(request);

        string body = await response.Content.ReadAsStringAsync();
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("not_found", body, StringComparison.Ordinal);
        Assert.Contains("undoCount", body, StringComparison.Ordinal);
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
