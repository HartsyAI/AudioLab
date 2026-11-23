using System.Text.Json;
using AudioLab.Core.Commands;
using AudioLab.Core.Model;
using AudioLab.Core.Realtime;
using AudioLab.Server.Mcp;
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
            Assert.Equal(McpToolHints.DescriptionOf(command), tool.Description);
            Assert.True(JsonElement.DeepEquals(command.Schema, tool.JsonSchema), $"{command.Name}: the schema differs from the registry's.");
        }
    }

    [Fact]
    public async Task EveryToolOfTheRealRegistryHasAValidNameAndASchemaWithoutRefPointers()
    {
        await using RunningServer server = await RunningServer.StartAsync();
        await using McpClient client = await McpTestClient.ConnectAsync(server);

        foreach (McpClientTool tool in await client.ListToolsAsync())
        {
            Assert.Matches("^[a-zA-Z0-9_-]{1,64}$", tool.Name);
            Assert.DoesNotContain("\"$ref\"", tool.JsonSchema.GetRawText(), StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task ACommandThatIsNoUndoStepSaysSoInItsDescriptionAndTheHintsFollowTheCommandsName()
    {
        await using RunningServer server = await RunningServer.StartAsync();
        CommandRegistry registry = server.Services.GetRequiredService<CommandRegistry>();
        await using McpClient client = await McpTestClient.ConnectAsync(server);
        IList<McpClientTool> tools = await client.ListToolsAsync();

        CommandDescriptor[] live = [.. registry.Commands.Where(command => !command.Undoable)];
        Assert.Contains(live, command => command.Name == "track_set_armed");
        Assert.Contains(live, command => command.Name == "track_set_monitor");
        foreach (CommandDescriptor command in registry.Commands)
        {
            string description = tools.Single(tool => tool.Name == command.Name).Description!;
            Assert.Equal(!command.Undoable, description.Contains(McpToolHints.NotUndoable, StringComparison.Ordinal));
        }

        ToolAnnotations Annotations(string name) => tools.Single(tool => tool.Name == name).ProtocolTool.Annotations!;
        foreach (string name in new[] { "track_remove", "marker_remove", "clip_delete", "midi_notes_delete", "group_ungroup", "score_clear" })
        {
            Assert.True(Annotations(name).DestructiveHint, name);
        }
        foreach (string name in new[] { "marker_add", "tempo_set_bpm", "marker_rename", "clip_move", "track_set_armed" })
        {
            Assert.False(Annotations(name).DestructiveHint, name);
        }
        foreach (string name in new[] { "tempo_set_bpm", "marker_rename", "marker_remove", "track_set_armed" })
        {
            Assert.True(Annotations(name).IdempotentHint, name);
        }
        foreach (string name in new[] { "marker_add", "track_add", "clip_split", "lyrics_shift", "midi_notes_transpose" })
        {
            Assert.False(Annotations(name).IdempotentHint, name);
        }
        Assert.All(registry.Commands, command => Assert.False(Annotations(command.Name).ReadOnlyHint));
        Assert.True(Annotations("undo").DestructiveHint);
        Assert.True(Annotations("get_project").ReadOnlyHint);
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

        Assert.True(McpTestClient.Reply(await McpTestClient.StepAsync(agent, "undo", project, 3)).Changed);
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
    public async Task AnAnswerToACommandIsNeverReplacedByTheResultLimitBecauseTheChangeIsMade()
    {
        Dictionary<string, string?> settings = new() { ["AudioLab:Mcp:MaxResultChars"] = "1024" };
        await using RunningServer server = await StartAsync(McpTestRegistry.Create(withFailures: true), settings);
        string project = await McpTestClient.CreateProjectAsync(server, UserId.Local);
        await using McpClient agent = await McpTestClient.ConnectAsync(server);

        CallToolResult added = await McpTestClient.ApplyAsync(agent, "add_many", project, ("count", 40));

        Assert.True(McpResults.Length(added) > 1024);
        Assert.NotEqual(true, added.IsError);
        CommandReply reply = McpTestClient.Reply(added);
        Assert.True(reply.Succeeded && reply.Changed);
        Assert.Equal(40, reply.Created.Length);
        Assert.Equal(2, reply.Revision);
        // A read of the same size is refused, so the limit is in force.
        Dictionary<string, object?> markers = new() { ["project"] = project, ["section"] = "markers" };
        CallToolResult read = await agent.CallToolAsync("get_project", markers);
        Assert.Equal(CommandErrorCode.Limit, Assert.Single(McpTestClient.Reply(read).Errors).Code);
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
    public async Task EveryCommandCallIsAppliedUnderAClientIdTheServerIssuedForIt()
    {
        RecordingClientIds issuer = new();
        await using RunningServer server = await McpTestClient.StartAsync(configureServices: services => services.AddSingleton<McpClientIds>(issuer));
        string project = await McpTestClient.CreateProjectAsync(server, UserId.Local);
        await using HubClient ui = await HubClient.ConnectAsync(server);
        await using McpClient agent = await McpTestClient.ConnectAsync(server);

        await McpTestClient.ApplyAsync(agent, "set_bpm", project, ("bpm", 90));
        await McpTestClient.ApplyAsync(agent, "add_marker", project, ("time", 1), ("name", "A"));
        await McpTestClient.ApplyAsync(agent, "add_marker", project, ("time", 2), ("name", "B"), ("client", "hub-1"), ("gesture", "g1"));
        await McpTestClient.CallAsync(agent, "get_history", project);
        await McpTestClient.ApplyAsync(agent, "set_bpm", "Not An Id", ("bpm", 90));

        // One id for each call that reached the project, none for a read or a call that named no project, and none the agent chose.
        ClientId[] issued = issuer.Issued();
        Assert.Equal(3, issued.Length);
        Assert.Equal(3, issued.Distinct().Count());
        Assert.All(issued, id => Assert.StartsWith(McpClientIds.Prefix, id.Value, StringComparison.Ordinal));
        Assert.DoesNotContain(issued, id => id.Value is "hub-1" || id.Value == ui.ConnectionId);
    }

    private sealed class RecordingClientIds : McpClientIds
    {
        private readonly List<ClientId> _issued = [];

        public ClientId[] Issued()
        {
            lock (_issued)
            {
                return [.. _issued];
            }
        }

        public override ClientId Issue()
        {
            ClientId id = base.Issue();
            lock (_issued)
            {
                _issued.Add(id);
            }
            return id;
        }
    }

    private static Task<RunningServer> StartAsync(CommandRegistry registry, IDictionary<string, string?>? settings = null)
    {
        return McpTestClient.StartAsync(settings, services => services.AddSingleton(registry));
    }
}
