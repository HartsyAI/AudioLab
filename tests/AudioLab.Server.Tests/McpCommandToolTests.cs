using System.Text.Json;
using AudioLab.Core.Commands;
using AudioLab.Core.Model;
using AudioLab.Server.Mcp;
using AudioLab.Server.Realtime;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace AudioLab.Server.Tests;

public sealed class McpCommandToolTests
{
    private static readonly string[] _readTools = ["get_history", "get_project", "list_projects", "redo", "undo"];

    [Fact]
    public async Task EveryCommandOfTheRealRegistryIsAToolWithItsNameDescriptionAndSchemaAsTheRegistryGivesThem()
    {
        await using RunningServer server = await RunningServer.StartAsync();
        CommandRegistry registry = server.Services.GetRequiredService<CommandRegistry>();
        await using McpClient client = await McpTestClient.ConnectAsync(server);

        IList<McpClientTool> tools = await client.ListToolsAsync();
        Assert.True(registry.Commands.Count > 100);
        string[] expected = [.. _readTools, .. registry.Commands.Select(command => command.Name)];
        string[] offered = [.. tools.Select(tool => tool.Name)];
        Assert.Equal(expected.Order(StringComparer.Ordinal).ToArray(), offered.Order(StringComparer.Ordinal).ToArray());
        foreach (CommandDescriptor command in registry.Commands)
        {
            McpClientTool tool = tools.Single(candidate => candidate.Name == command.Name);
            Assert.Equal(command.Description, tool.Description);
            Assert.True(JsonElement.DeepEquals(command.Schema, tool.JsonSchema), $"{command.Name}: the schema differs from the registry's.");
        }
    }

    [Fact]
    public async Task AddingACommandToTheRegistryAddsItsToolAndNothingElseAndItCanBeCalled()
    {
        await using RunningServer before = await StartAsync(McpTestRegistry.Create(withMarker: false));
        await using RunningServer after = await StartAsync(McpTestRegistry.Create(withMarker: true));
        await using McpClient clientBefore = await McpTestClient.ConnectAsync(before);
        await using McpClient clientAfter = await McpTestClient.ConnectAsync(after);

        string[] without = [.. (await clientBefore.ListToolsAsync()).Select(tool => tool.Name)];
        IList<McpClientTool> with = await clientAfter.ListToolsAsync();
        Assert.Equal(["add_marker"], with.Select(tool => tool.Name).Except(without).ToArray());
        Assert.Empty(without.Except(with.Select(tool => tool.Name)));
        McpClientTool added = with.Single(tool => tool.Name == "add_marker");
        Assert.Equal("Adds a marker and allocates its id.", added.Description);
        JsonElement properties = added.JsonSchema.GetProperty("properties");
        string[] members = [.. properties.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal)];
        Assert.Equal(["name", "project", "time"], members);

        string project = await McpTestClient.CreateProjectAsync(after, UserId.Local);
        CallToolResult called = await McpTestClient.ApplyAsync(clientAfter, "add_marker", project, ("time", 4), ("name", "Drop"));
        CommandReply reply = McpTestClient.Reply(called);
        Assert.True(reply.Succeeded && reply.Changed);
        Assert.Single(reply.Created);
        await Assert.ThrowsAsync<McpProtocolException>(() => McpTestClient.ApplyAsync(clientBefore, "add_marker", project).AsTask());
    }

    [Fact]
    public async Task ACommandToolAppliesTheCommandThroughTheHubsPathAndEveryClientHearsIt()
    {
        await using RunningServer server = await McpTestClient.StartAsync();
        string project = await McpTestClient.CreateProjectAsync(server, UserId.Local);
        await using HubClient ui = await HubClient.ConnectAsync(server);
        await ui.JoinAsync(project);
        await using McpClient agent = await McpTestClient.ConnectAsync(server);

        CommandReply tempo = McpTestClient.Reply(await McpTestClient.ApplyAsync(agent, "set_bpm", project, ("bpm", 90.5)));
        Assert.True(tempo.Succeeded && tempo.Changed);
        Assert.Equal(2, tempo.Revision);
        ProjectChange change = await ui.NextChangeAsync();
        Assert.Equal((project, 2L, "set_bpm"), (change.Project, change.Revision, change.Reason));
        Assert.Equal(90.5, ProjectJson.Read(change.Document).Bpm);

        CallToolResult added = await McpTestClient.ApplyAsync(agent, "add_marker", project, ("time", 4), ("name", "Drop"));
        CommandReply marker = McpTestClient.Reply(added);
        Assert.Equal(marker.Created[0], ProjectJson.Read((await ui.NextChangeAsync()).Document).Markers[0].Id.Value);
        CallToolResult history = await McpTestClient.CallAsync(agent, "get_history", project);
        Assert.Equal(2, McpTestClient.Reply(history, McpJsonContext.Default.HistoryState).UndoCount);

        CommandReply same = McpTestClient.Reply(await McpTestClient.ApplyAsync(agent, "set_bpm", project, ("bpm", 90.5)));
        Assert.True(same.Succeeded);
        Assert.False(same.Changed);
        Assert.True(await ui.HeardNothingAsync());

        Assert.True(McpTestClient.Reply(await McpTestClient.CallAsync(agent, "undo", project)).Changed);
        Assert.Equal("undo", (await ui.NextChangeAsync()).Reason);
    }

    [Fact]
    public async Task ARealCoreCommandRunsOverMcpAndAnAgentCannotNameAClientOrAGesture()
    {
        await using RunningServer server = await RunningServer.StartAsync();
        string project = await McpTestClient.CreateProjectAsync(server, UserId.Local);
        await using HubClient ui = await HubClient.ConnectAsync(server);
        await ui.JoinAsync(project);
        await using McpClient agent = await McpTestClient.ConnectAsync(server);

        CallToolResult added = await McpTestClient.ApplyAsync(agent, "marker_add", project, ("time", 2.5), ("name", "Verse"));
        CommandReply marker = McpTestClient.Reply(added);
        Assert.True(marker.Succeeded);
        ProjectDocument document = ProjectJson.Read((await ui.NextChangeAsync()).Document);
        Assert.Equal((marker.Created[0], "Verse"), (document.Markers[0].Id.Value, document.Markers[0].Name));

        foreach (string member in new[] { "client", "gesture", "owner", "revision" })
        {
            CallToolResult result = await McpTestClient.ApplyAsync(agent, "marker_add", project, ("time", 3), ("name", "Chorus"), (member, "x"));
            Assert.True(result.IsError);
            Assert.Equal(CommandErrorCode.Malformed, Assert.Single(McpTestClient.Reply(result).Errors).Code);
        }
        Assert.True(await ui.HeardNothingAsync());
    }

    [Fact]
    public async Task EveryRefusalClassACommandCanReturnReachesTheAgentAsAnErrorResultWithItsClass()
    {
        await using RunningServer server = await StartAsync(McpTestRegistry.Create(withFailures: true));
        string project = await McpTestClient.CreateProjectAsync(server, UserId.Local);
        await using HubClient ui = await HubClient.ConnectAsync(server);
        await ui.JoinAsync(project);
        await using McpClient agent = await McpTestClient.ConnectAsync(server);

        foreach (CommandErrorCode code in Enum.GetValues<CommandErrorCode>())
        {
            string wire = McpTestRegistry.Wire(code);
            CallToolResult result = await McpTestClient.ApplyAsync(agent, "fail_with", project, ("code", wire));
            Assert.True(result.IsError);
            CommandReply reply = McpTestClient.Reply(result);
            Assert.False(reply.Succeeded);
            Assert.Equal(new HubCommandError(code, $"Refused as {wire}."), Assert.Single(reply.Errors));
            Assert.Equal(1, reply.Revision);
        }
        CallToolResult invalid = await McpTestClient.ApplyAsync(agent, "bad_bpm", project);
        Assert.True(invalid.IsError);
        Assert.Equal(CommandErrorCode.Invalid, McpTestClient.Reply(invalid).Errors[0].Code);
        CommandReply nothing = McpTestClient.Reply(await McpTestClient.ApplyAsync(agent, "no_change", project));
        Assert.True(nothing.Succeeded);
        Assert.False(nothing.Changed);
        Assert.True(await ui.HeardNothingAsync());
    }

    [Fact]
    public async Task ACommandThatWouldMakeTheProjectTooLargeIsRefusedWithLimit()
    {
        Dictionary<string, string?> settings = new() { ["AudioLab:Storage:MaxFileBytes"] = "3000" };
        await using RunningServer server = await McpTestClient.StartAsync(settings);
        string project = await McpTestClient.CreateProjectAsync(server, UserId.Local);
        await using McpClient agent = await McpTestClient.ConnectAsync(server);

        CommandReply last = new(true, true, 1, [], []);
        for (int index = 0; index < 60 && last.Succeeded; index++)
        {
            last = McpTestClient.Reply(await McpTestClient.ApplyAsync(agent, "add_marker", project, ("time", index), ("name", "A marker")));
        }
        Assert.False(last.Succeeded);
        Assert.Equal(CommandErrorCode.Limit, Assert.Single(last.Errors).Code);
    }

    [Fact]
    public async Task ABadCommandIsRefusedBeforeAnythingChangesAndAnotherUsersProjectIsNotFound()
    {
        await using RunningServer server = await McpTestClient.StartAsync(McpTestClient.SwarmSettings());
        string alices = await McpTestClient.CreateProjectAsync(server, McpTestClient.SwarmUser("alice"));
        await using McpClient alice = await McpTestClient.ConnectAsync(server, McpTestClient.AsUser("alice"));
        await using McpClient bob = await McpTestClient.ConnectAsync(server, McpTestClient.AsUser("bob"));

        Dictionary<string, object?>[] malformed =
        [
            [],
            new() { ["bpm"] = 90 },
            new() { ["project"] = 5, ["bpm"] = 90 },
            new() { ["project"] = "Not An Id", ["bpm"] = 90 },
            new() { ["project"] = alices },
            new() { ["project"] = alices, ["bpm"] = "fast" },
            new() { ["project"] = alices, ["bpm"] = null },
            new() { ["project"] = alices, ["bpm"] = 90, ["extra"] = true },
        ];
        foreach (Dictionary<string, object?> arguments in malformed)
        {
            CallToolResult result = await alice.CallToolAsync("set_bpm", arguments);
            Assert.True(result.IsError);
            Assert.Equal(CommandErrorCode.Malformed, Assert.Single(McpTestClient.Reply(result).Errors).Code);
        }

        CallToolResult theirs = await McpTestClient.ApplyAsync(bob, "set_bpm", alices, ("bpm", 90));
        CallToolResult none = await McpTestClient.ApplyAsync(bob, "set_bpm", "nosuchproject", ("bpm", 90));
        Assert.Equal(CommandErrorCode.NotFound, Assert.Single(McpTestClient.Reply(theirs).Errors).Code);
        Assert.Equal(McpTestClient.Text(none), McpTestClient.Text(theirs));
        CallToolResult history = await McpTestClient.CallAsync(alice, "get_history", alices);
        Assert.Equal(1, McpTestClient.Reply(history, McpJsonContext.Default.HistoryState).Revision);
    }

    [Fact]
    public async Task ACommandWithTheNameOfAToolThatIsNotACommandStopsTheServerFromStarting()
    {
        foreach (string name in new[] { "undo", "redo", "list_projects", "get_project", "get_history" })
        {
            InvalidOperationException error = await Assert.ThrowsAnyAsync<InvalidOperationException>(
                () => StartAsync(McpTestRegistry.Create(extraName: name)));
            Assert.Contains(name, error.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void AnAgentsClientIdIsAValidServerIssuedIdThatNoHubConnectionCanHave()
    {
        ClientId client = ClientId.From(McpToolService.ClientPrefix + Guid.NewGuid().ToString("N"));

        Assert.StartsWith("mcp-", client.Value, StringComparison.Ordinal);
        Assert.Equal(McpToolService.ClientPrefix.Length + 32, client.Value.Length);
    }

    private static Task<RunningServer> StartAsync(CommandRegistry registry)
    {
        return McpTestClient.StartAsync(configureServices: services => services.AddSingleton(registry));
    }
}
