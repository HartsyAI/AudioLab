using AudioLab.Core.Api;
using AudioLab.Core.Commands;
using AudioLab.Core.Model;
using AudioLab.Core.Realtime;
using AudioLab.Server.Realtime;
using AudioLab.Server.Services;
using AudioLab.Server.Store;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

namespace AudioLab.Server.Tests;

public sealed class HubTests
{
    private const string Secret = "0123456789abcdef0123456789abcdef";

    [Fact]
    public async Task EveryClientOfAProjectHearsEveryChangeAndAClientOutsideItHearsNothing()
    {
        await using RunningServer server = await StartAsync();
        string project = await CreateAsync(server, UserId.Local);
        await using HubClient editor = await HubClient.ConnectAsync(server);
        await using HubClient agent = await HubClient.ConnectAsync(server);
        await using HubClient outsider = await HubClient.ConnectAsync(server);
        JoinReply joined = await editor.JoinAsync(project);
        await agent.JoinAsync(project);
        Assert.Equal(1, joined.Revision);
        Assert.Equal("Song", ProjectJson.Read(joined.Document).Name);

        CommandReply renamed = await editor.ApplyAsync(project, "rename_project", Json(project, "\"name\":\"Renamed\""));
        Assert.True(renamed.Succeeded && renamed.Changed);
        Assert.Equal(2, renamed.Revision);
        foreach (HubClient client in new[] { editor, agent })
        {
            ProjectChange change = await client.NextChangeAsync();
            Assert.Equal((project, 2L, "rename_project"), (change.Project, change.Revision, change.Reason));
            Assert.Equal("Renamed", ProjectJson.Read(change.Document).Name);
        }

        CommandReply marked = await agent.ApplyAsync(project, "add_marker", Json(project, "\"time\":4,\"name\":\"Drop\""));
        Assert.Single(marked.Created);
        foreach (HubClient client in new[] { editor, agent })
        {
            ProjectChange change = await client.NextChangeAsync();
            Assert.Equal(3, change.Revision);
            Assert.Equal(marked.Created[0], ProjectJson.Read(change.Document).Markers[0].Id.Value);
        }
        Assert.True(await outsider.HeardNothingAsync());
    }

    [Fact]
    public async Task ACommandThatIsRefusedUnknownMalformedOrChangesNothingIsAReplyAndNobodyHearsIt()
    {
        await using RunningServer server = await StartAsync();
        string project = await CreateAsync(server, UserId.Local);
        await using HubClient a = await HubClient.ConnectAsync(server);
        await using HubClient b = await HubClient.ConnectAsync(server);
        await a.JoinAsync(project);
        await b.JoinAsync(project);

        CommandReply refused = await a.ApplyAsync(project, "refuse", Json(project, string.Empty));
        Assert.False(refused.Succeeded);
        Assert.Equal(CommandErrorCode.Rejected, refused.Errors.Single().Code);
        Assert.Equal(CommandErrorCode.UnknownCommand, (await a.ApplyAsync(project, "no_such", "{}")).Errors.Single().Code);
        Assert.Equal(CommandErrorCode.Malformed, (await a.ApplyAsync(project, "rename_project", "{\"project\":\"" + project + "\",\"nom\":1}")).Errors.Single().Code);
        Assert.Equal(CommandErrorCode.WrongProject, (await a.ApplyAsync(project, "no_change", Json("prj_other", string.Empty))).Errors.Single().Code);

        CommandReply nothing = await a.ApplyAsync(project, "no_change", Json(project, string.Empty));
        Assert.True(nothing.Succeeded);
        Assert.False(nothing.Changed);
        Assert.Equal(1, nothing.Revision);
        Assert.True(await a.HeardNothingAsync() && await b.HeardNothingAsync());
    }

    [Fact]
    public async Task UndoAndRedoAreHeardByEveryoneAndNothingToUndoIsAReply()
    {
        await using RunningServer server = await StartAsync();
        string project = await CreateAsync(server, UserId.Local);
        await using HubClient a = await HubClient.ConnectAsync(server);
        await using HubClient b = await HubClient.ConnectAsync(server);
        await a.JoinAsync(project);
        await b.JoinAsync(project);
        Assert.False((await a.UndoAsync(project)).Succeeded);
        Assert.False((await a.RedoAsync(project)).Succeeded);

        await a.ApplyAsync(project, "rename_project", Json(project, "\"name\":\"Two\""));
        await a.NextChangeAsync();
        await b.NextChangeAsync();
        Assert.True((await b.UndoAsync(project)).Changed);
        foreach (HubClient client in new[] { a, b })
        {
            ProjectChange change = await client.NextChangeAsync();
            Assert.Equal(("undo", "Song"), (change.Reason, ProjectJson.Read(change.Document).Name));
        }
        Assert.True((await a.RedoAsync(project)).Changed);
        foreach (HubClient client in new[] { a, b })
        {
            ProjectChange change = await client.NextChangeAsync();
            Assert.Equal(("redo", "Two"), (change.Reason, ProjectJson.Read(change.Document).Name));
        }
    }

    [Fact]
    public async Task AGestureIsOneUndoStepAndEveryCommandInItIsHeard()
    {
        await using RunningServer server = await StartAsync();
        string project = await CreateAsync(server, UserId.Local);
        await using HubClient a = await HubClient.ConnectAsync(server);
        await using HubClient b = await HubClient.ConnectAsync(server);
        await a.JoinAsync(project);
        await b.JoinAsync(project);
        foreach (int bpm in new[] { 100, 110, 120 })
        {
            await a.ApplyAsync(project, "set_bpm", Json(project, $"\"bpm\":{bpm + 1}"), "drag_1");
            Assert.Equal(bpm + 1, ProjectJson.Read((await b.NextChangeAsync()).Document).Bpm);
            await a.NextChangeAsync();
        }
        await a.EndGestureAsync(project, "drag_1");
        Assert.True((await a.UndoAsync(project)).Changed);
        Assert.Equal(NewProjectFactory.StartingBpm, ProjectJson.Read((await b.NextChangeAsync()).Document).Bpm);
        Assert.False((await a.UndoAsync(project)).Succeeded);
    }

    [Fact]
    public async Task ADisconnectedClientsGestureIsClosedAndItsClientIdIsTheOneTheServerIssued()
    {
        await using RunningServer server = await StartAsync();
        string project = await CreateAsync(server, UserId.Local);
        HubClient a = await HubClient.ConnectAsync(server);
        await a.JoinAsync(project);
        await a.ApplyAsync(project, "set_bpm", Json(project, "\"bpm\":99"), "drag_1");
        ProjectSession session = server.Services.GetRequiredService<OpenProjects>().SessionIfOpen(UserId.Local, ProjectId.From(project))!;
        ClientId client = ClientId.From(a.ConnectionId);
        Assert.NotNull(session.OpenGestureOf(client));

        await a.DisposeAsync();
        for (int i = 0; i < 100 && session.OpenGestureOf(client) is not null; i++)
        {
            await Task.Delay(50);
        }
        Assert.Null(session.OpenGestureOf(client));
        Assert.Equal(99, session.Snapshot.Document.Bpm);
    }

    [Fact]
    public async Task RevisionsReachEveryClientInIncreasingOrderAndTheLastOneAlwaysArrivesWhileTwoClientsEditAtOnce()
    {
        await using RunningServer server = await StartAsync();
        string project = await CreateAsync(server, UserId.Local);
        await using HubClient a = await HubClient.ConnectAsync(server);
        await using HubClient b = await HubClient.ConnectAsync(server);
        await a.JoinAsync(project);
        await b.JoinAsync(project);
        const int Each = 25;
        Task Edit(HubClient client, int offset) => Task.Run(async () =>
        {
            for (int i = 0; i < Each; i++)
            {
                CommandReply reply = await client.ApplyAsync(project, "add_marker", Json(project, $"\"time\":{offset + i},\"name\":\"m{offset + i}\""));
                Assert.True(reply.Succeeded);
            }
        });
        await Task.WhenAll(Edit(a, 0), Edit(b, 1000));

        foreach (HubClient client in new[] { a, b })
        {
            long last = 1;
            while (last < (2 * Each) + 1)
            {
                ProjectChange change = await client.NextChangeAsync();
                Assert.True(change.Revision > last, $"revision {change.Revision} came after {last}");
                Assert.Equal(change.Revision - 1, ProjectJson.Read(change.Document).Markers.Count);
                last = change.Revision;
            }
        }
    }

    [Fact]
    public async Task AClientThatJoinsWhileOthersEditHearsOnlyNewerRevisionsAndTheLastOne()
    {
        await using RunningServer server = await StartAsync();
        string project = await CreateAsync(server, UserId.Local);
        await using HubClient editor = await HubClient.ConnectAsync(server);
        await using HubClient late = await HubClient.ConnectAsync(server);
        await editor.JoinAsync(project);
        const int Edits = 60;
        Task editing = Task.Run(async () =>
        {
            for (int i = 0; i < Edits; i++)
            {
                await editor.ApplyAsync(project, "set_bpm", Json(project, $"\"bpm\":{100 + i}"));
            }
        });
        await Task.Delay(20);
        JoinReply joined = await late.JoinAsync(project);
        await editing;

        long last = joined.Revision;
        while (last < Edits + 1)
        {
            ProjectChange change = await late.NextChangeAsync();
            Assert.True(change.Revision > last, $"revision {change.Revision} came after {last}");
            last = change.Revision;
        }
        Assert.True(await late.HeardNothingAsync());
    }

    [Fact]
    public async Task LeavingStopsTheChangesAndAClientMustJoinBeforeItCanEdit()
    {
        await using RunningServer server = await StartAsync();
        string project = await CreateAsync(server, UserId.Local);
        await using HubClient a = await HubClient.ConnectAsync(server);
        await using HubClient b = await HubClient.ConnectAsync(server);
        HubException notJoined = await Assert.ThrowsAsync<HubException>(() => a.ApplyAsync(project, "no_change", Json(project, string.Empty)));
        Assert.Contains("Join the project first", notJoined.Message, StringComparison.Ordinal);
        await a.JoinAsync(project);
        await b.JoinAsync(project);
        await b.LeaveAsync(project);
        await a.ApplyAsync(project, "rename_project", Json(project, "\"name\":\"After\""));
        await a.NextChangeAsync();
        Assert.True(await b.HeardNothingAsync());
        await Assert.ThrowsAsync<HubException>(() => b.ApplyAsync(project, "no_change", Json(project, string.Empty)));
    }

    [Fact]
    public async Task DeletingAProjectTellsItsClientsAndNoEditGetsThrough()
    {
        await using RunningServer server = await StartAsync();
        string project = await CreateAsync(server, UserId.Local);
        await using HubClient a = await HubClient.ConnectAsync(server);
        await using HubClient b = await HubClient.ConnectAsync(server);
        await a.JoinAsync(project);
        await b.JoinAsync(project);
        Assert.Equal(System.Net.HttpStatusCode.NoContent, (await server.Client.DeleteAsync($"/api/projects/{project}")).StatusCode);
        foreach (HubClient client in new[] { a, b })
        {
            Assert.Equal(new ProjectClosed(project, "deleted"), await client.NextClosedAsync());
        }
        HubException error = await Assert.ThrowsAsync<HubException>(() => a.ApplyAsync(project, "no_change", Json(project, string.Empty)));
        Assert.Contains("Join the project first", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AUserCannotJoinOrWatchAnotherUsersProject()
    {
        await using RunningServer server = await StartAsync(SwarmSettings());
        UserId alice = ProxyDoor("alice");
        string project = await CreateAsync(server, alice);
        await using HubClient owner = await HubClient.ConnectAsync(server, options => Proxy(options, "alice"));
        await using HubClient other = await HubClient.ConnectAsync(server, options => Proxy(options, "bob"));
        await owner.JoinAsync(project);

        HubException refused = await Assert.ThrowsAsync<HubException>(() => other.JoinAsync(project));
        Assert.Contains("no such project", refused.Message, StringComparison.Ordinal);
        await Assert.ThrowsAsync<HubException>(() => other.ApplyAsync(project, "rename_project", Json(project, "\"name\":\"Mine now\"")));
        await Assert.ThrowsAsync<HubException>(() => other.UndoAsync(project));
        await Assert.ThrowsAsync<HubException>(() => other.JoinAsync("../alice"));

        await owner.ApplyAsync(project, "rename_project", Json(project, "\"name\":\"Still Alice's\""));
        await owner.NextChangeAsync();
        Assert.True(await other.HeardNothingAsync());
        Assert.Equal("Still Alice's", server.Services.GetRequiredService<OpenProjects>().SnapshotIfOpen(alice, ProjectId.From(project))!.Document.Name);
    }

    [Fact]
    public async Task AConnectionMayJoinOnlyAFewProjects()
    {
        await using RunningServer server = await StartAsync();
        await using HubClient client = await HubClient.ConnectAsync(server);
        List<string> projects = [];
        for (int i = 0; i <= ProjectHub.MaxProjectsPerConnection; i++)
        {
            projects.Add(await CreateAsync(server, UserId.Local));
        }
        foreach (string project in projects.Take(ProjectHub.MaxProjectsPerConnection))
        {
            await client.JoinAsync(project);
        }
        await client.JoinAsync(projects[0]);
        await Assert.ThrowsAsync<HubException>(() => client.JoinAsync(projects[^1]));
    }

    [Fact]
    public async Task AMessageOverTheLimitClosesTheConnection()
    {
        await using RunningServer server = await StartAsync(new Dictionary<string, string?> { ["AudioLab:HubMaxCommandChars"] = "1000" });
        string project = await CreateAsync(server, UserId.Local);
        await using HubClient client = await HubClient.ConnectAsync(server);
        await client.JoinAsync(project);
        await Assert.ThrowsAnyAsync<Exception>(() => client.ApplyAsync(project, "rename_project", Json(project, "\"name\":\"" + new string('x', 20_000) + "\"")));
        for (int i = 0; i < 100 && client.Connection.State == HubConnectionState.Connected; i++)
        {
            await Task.Delay(50);
        }
        Assert.NotEqual(HubConnectionState.Connected, client.Connection.State);
    }

    [Fact]
    public async Task AnEditMadeThroughTheHubIsAutosavedLikeAnyOther()
    {
        await using RunningServer server = await StartAsync(new Dictionary<string, string?> { ["AudioLab:Storage:AutosaveDelay"] = "00:00:00.100", ["AudioLab:Storage:AutosaveMaxDelay"] = "00:00:00.500" });
        string project = await CreateAsync(server, UserId.Local);
        await using HubClient client = await HubClient.ConnectAsync(server);
        await client.JoinAsync(project);
        await client.ApplyAsync(project, "rename_project", Json(project, "\"name\":\"Saved by autosave\""));
        ProjectSession session = server.Services.GetRequiredService<OpenProjects>().SessionIfOpen(UserId.Local, ProjectId.From(project))!;
        for (int i = 0; i < 100 && session.IsUnsaved; i++)
        {
            await Task.Delay(50);
        }
        Assert.False(session.IsUnsaved);
        string file = Directory.GetFiles(server.StoreFolder, "p*.json", SearchOption.AllDirectories).Single();
        Assert.Equal("Saved by autosave", ProjectJson.Read(await File.ReadAllTextAsync(file)).Name);
    }

    [Fact]
    public async Task ANullForAnyArgumentOfAnyMethodIsRefusedWithoutAnErrorAndTheConnectionLives()
    {
        await using RunningServer server = await StartAsync();
        string project = await CreateAsync(server, UserId.Local);
        await using HubClient client = await HubClient.ConnectAsync(server);
        await using HubClient listener = await HubClient.ConnectAsync(server);
        await client.JoinAsync(project);
        await listener.JoinAsync(project);
        string json = Json(project, string.Empty);

        object?[][] calls =
        [
            ["Join", null],
            ["Leave", null],
            ["Apply", null, "no_change", json, null],
            ["Apply", project, null, json, null],
            ["Apply", project, "no_change", null, null],
            ["Apply", project, "no_change", json, null],
            ["Undo", null],
            ["Redo", null],
            ["EndGesture", null, "g"],
            ["EndGesture", project, null],
        ];
        foreach (object?[] call in calls)
        {
            string method = (string)call[0]!;
            try
            {
                await client.Connection.InvokeCoreAsync<object?>(method, [.. call.Skip(1)]);
            }
            catch (HubException)
            {
                // A short refusal is the expected answer.
            }
            Assert.Equal(HubConnectionState.Connected, client.Connection.State);
        }
        Assert.True(await listener.HeardNothingAsync());
        Assert.True((await client.ApplyAsync(project, "no_change", json)).Succeeded);
    }

    [Fact]
    public async Task TheHubWorksUnderAPathPrefix()
    {
        await using RunningServer server = await StartAsync(new Dictionary<string, string?> { ["AudioLab:PathBase"] = "/audiolab" });
        Assert.Equal("/audiolab/api/hub", server.HubPath);
        string project = await CreateAsync(server, UserId.Local);
        await using HubClient a = await HubClient.ConnectAsync(server);
        await using HubClient b = await HubClient.ConnectAsync(server);
        await a.JoinAsync(project);
        await b.JoinAsync(project);
        await a.ApplyAsync(project, "rename_project", Json(project, "\"name\":\"Prefixed\""));
        Assert.Equal("Prefixed", ProjectJson.Read((await b.NextChangeAsync()).Document).Name);
    }

    [Fact]
    public async Task TheRealServerAcceptsTheDomainCommandsCoreShips()
    {
        await using RunningServer server = await RunningServer.StartAsync();
        string project = await CreateAsync(server, UserId.Local);
        await using HubClient a = await HubClient.ConnectAsync(server);
        await using HubClient b = await HubClient.ConnectAsync(server);
        await a.JoinAsync(project);
        await b.JoinAsync(project);

        CommandReply added = await a.ApplyAsync(project, "marker_add", Json(project, "\"time\":12.5,\"name\":\"Chorus\""));
        Assert.True(added.Succeeded, string.Join("; ", added.Errors.Select(error => error.Message)));
        Assert.Single(added.Created);
        ProjectDocument heard = ProjectJson.Read((await b.NextChangeAsync()).Document);
        Assert.Equal(("Chorus", 12.5), (heard.Markers[0].Name, heard.Markers[0].Time));
        Assert.Equal(added.Created[0], heard.Markers[0].Id.Value);
        Assert.Equal(AudioLab.Core.Commands.CommandErrorCode.UnknownCommand, (await a.ApplyAsync(project, "no_such_command", "{}")).Errors.Single().Code);
    }

    private static Task<RunningServer> StartAsync(IDictionary<string, string?>? settings = null)
    {
        return RunningServer.StartAsync(settings, services => services.AddSingleton(TestCommandRegistry.Create()));
    }

    private static Dictionary<string, string?> SwarmSettings()
    {
        return new Dictionary<string, string?> { ["AudioLab:Door:Kind"] = "Swarm", ["AudioLab:Door:ProxySecret"] = Secret };
    }

    private static UserId ProxyDoor(string subject) => AudioLab.Server.Doors.ProxyDoor.UserFor("swarm", subject);

    private static void Proxy(Microsoft.AspNetCore.Http.Connections.Client.HttpConnectionOptions options, string subject)
    {
        options.Headers["X-AudioLab-Secret"] = Secret;
        options.Headers["X-AudioLab-User"] = subject;
    }

    private static async Task<string> CreateAsync(RunningServer server, UserId user)
    {
        ProjectSummary created = await server.Services.GetRequiredService<ProjectService>().CreateAsync(user, "Song", CancellationToken.None);
        return created.Id;
    }

    private static string Json(string project, string members)
    {
        return "{\"project\":\"" + project + "\"" + (members.Length == 0 ? string.Empty : "," + members) + "}";
    }
}
