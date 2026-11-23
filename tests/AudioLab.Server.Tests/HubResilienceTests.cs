using System.Diagnostics;
using AudioLab.Core.Model;
using AudioLab.Core.Realtime;
using AudioLab.Server.Realtime;
using AudioLab.Server.Services;
using AudioLab.Server.Store;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

namespace AudioLab.Server.Tests;

public sealed class HubResilienceTests
{
    [Fact]
    public async Task AClientThatStopsReadingDoesNotSlowTheEditorsOrTheOtherClientsAndIsDisconnected()
    {
        await using RunningServer server = await StartAsync(new Dictionary<string, string?> { ["AudioLab:HubSendTimeout"] = "00:00:01" });
        string project = await CreateAsync(server);
        await FillAsync(server, project, 30_000);
        await using HubClient editor = await HubClient.ConnectAsync(server);
        await using HubClient reader = await HubClient.ConnectAsync(server);
        await editor.JoinAsync(project);
        await reader.JoinAsync(project);
        await using RawHubSocket stalled = await RawHubSocket.JoinAsync(server, project);
        ProjectMembers members = server.Services.GetRequiredService<ProjectMembers>();
        Assert.Equal(3, members.Of(UserId.Local, ProjectId.From(project)).Count);

        Stopwatch slowest = new();
        const int Edits = 30;
        for (int i = 0; i < Edits; i++)
        {
            slowest.Restart();
            CommandReply reply = await editor.ApplyAsync(project, "set_bpm", Json(project, $"\"bpm\":{100 + i}"));
            Assert.True(reply.Succeeded);
            Assert.True(slowest.Elapsed < TimeSpan.FromSeconds(5), $"edit {i} waited {slowest.Elapsed} for a client that was not reading");
        }

        long last = 0;
        while (last < Edits + 1)
        {
            last = (await reader.NextChangeAsync()).Revision;
        }
        Assert.Equal(Edits + 1, last);
        Assert.True(await stalled.ServerClosedAsync(TimeSpan.FromSeconds(30)), "the server kept a client that took nothing");
        for (int i = 0; i < 100 && members.Of(UserId.Local, ProjectId.From(project)).Count > 2; i++)
        {
            await Task.Delay(100);
        }
        Assert.Equal(2, members.Of(UserId.Local, ProjectId.From(project)).Count);
        Assert.True((await editor.ApplyAsync(project, "set_bpm", Json(project, "\"bpm\":77"))).Changed);
    }

    [Theory]
    [InlineData("a", 500_000)]
    [InlineData("\u00e9", 500_000)]
    [InlineData("\\\"", 250_000)]
    public async Task ACommandOfHalfAMegabyteIsAcceptedWhateverCharactersItHolds(string unit, int count)
    {
        await using RunningServer server = await StartAsync();
        string project = await CreateAsync(server);
        await using HubClient client = await HubClient.ConnectAsync(server);
        await client.JoinAsync(project);
        string text = string.Concat(Enumerable.Repeat(unit, count));
        CommandReply reply = await client.ApplyAsync(project, "bulk", Json(project, "\"text\":\"" + text + "\""));
        Assert.True(reply.Succeeded, string.Join("; ", reply.Errors.Select(error => error.Message)));
        Assert.Equal(HubConnectionState.Connected, client.Connection.State);
    }

    [Fact]
    public async Task ACommandOverTheHubLimitIsRefusedWithATypedErrorAndTheConnectionStays()
    {
        await using RunningServer server = await StartAsync(new Dictionary<string, string?> { ["AudioLab:HubMaxCommandChars"] = "1000" });
        string project = await CreateAsync(server);
        await using HubClient client = await HubClient.ConnectAsync(server);
        await client.JoinAsync(project);
        CommandReply reply = await client.ApplyAsync(project, "bulk", Json(project, "\"text\":\"" + new string('x', 3000) + "\""));
        Assert.Equal(AudioLab.Core.Commands.CommandErrorCode.Limit, reply.Errors.Single().Code);
        Assert.Equal(HubConnectionState.Connected, client.Connection.State);
        Assert.True((await client.ApplyAsync(project, "no_change", Json(project, string.Empty))).Succeeded);
    }

    [Fact]
    public async Task ADeletedProjectIsForgottenByTheConnectionSoItsSlotIsFreeAgain()
    {
        await using RunningServer server = await StartAsync();
        await using HubClient client = await HubClient.ConnectAsync(server);
        List<string> projects = [];
        for (int i = 0; i <= ProjectHub.MaxProjectsPerConnection; i++)
        {
            projects.Add(await CreateAsync(server));
        }
        foreach (string project in projects.Take(ProjectHub.MaxProjectsPerConnection))
        {
            await client.JoinAsync(project);
        }
        await Assert.ThrowsAsync<HubException>(() => client.JoinAsync(projects[^1]));
        Assert.Equal(System.Net.HttpStatusCode.NoContent, (await server.Client.DeleteAsync($"/api/projects/{projects[0]}")).StatusCode);
        await client.NextClosedAsync();
        await client.JoinAsync(projects[^1]);
    }

    [Fact]
    public async Task EveryEndpointOfTheServerNeedsTheDoorExceptTheHealthRoute()
    {
        await using RunningServer server = await StartAsync();
        IEnumerable<Microsoft.AspNetCore.Http.Endpoint> all = server.Services.GetServices<EndpointDataSource>().SelectMany(source => source.Endpoints);
        List<RouteEndpoint> endpoints = [.. all.OfType<RouteEndpoint>()];
        bool NeedsDoor(RouteEndpoint endpoint) => endpoint.Metadata.GetMetadata<AudioLab.Server.Doors.DoorRequired>() is not null;
        bool IsUi(RouteEndpoint endpoint) => endpoint.Metadata.GetMetadata<AudioLab.Server.Routes.UiEndpoint>() is not null;
        IEnumerable<RouteEndpoint> unguarded = endpoints.Where(endpoint => !NeedsDoor(endpoint) && !IsUi(endpoint));
        List<string> open = [.. unguarded.Select(endpoint => endpoint.RoutePattern.RawText ?? "?")];
        Assert.Equal(["/health"], open);
        Assert.Equal(["GET"], endpoints.Single(endpoint => endpoint.RoutePattern.RawText == "/health").Metadata
            .GetMetadata<HttpMethodMetadata>()!.HttpMethods);
        Assert.DoesNotContain(endpoints, endpoint => IsUi(endpoint) && NeedsDoor(endpoint));
        List<RouteEndpoint> ui = [.. endpoints.Where(IsUi)];
        // Two wildcards only: the start page for any address that is not a file, and the static files' own catch-all for one that is.
        // Every other open endpoint is a static file at a fixed address.
        IEnumerable<string> patterns = ui.Select(endpoint => endpoint.RoutePattern.RawText ?? "?");
        IEnumerable<string> wildcards = patterns.Where(pattern => pattern.Contains('{', StringComparison.Ordinal)).Distinct().Order();
        Assert.Equal(["/{**path:file}"], wildcards);
        Assert.DoesNotContain(ui, endpoint => OutsideTheProgram(endpoint.RoutePattern.RawText ?? "?"));
        Assert.All(ui, endpoint => Assert.All(
            endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? ["(any method)"],
            method => Assert.Contains(method, new[] { "GET", "HEAD" })));
        Assert.True(ui.Count >= 5, "too few UI endpoints were found for this check to mean anything");
        Assert.True(endpoints.Count >= 8, "too few endpoints were found for this check to mean anything");
    }

    private static bool OutsideTheProgram(string pattern)
    {
        return pattern.StartsWith("/api", StringComparison.OrdinalIgnoreCase) || pattern.StartsWith("/health", StringComparison.OrdinalIgnoreCase);
    }

    private static Task<RunningServer> StartAsync(IDictionary<string, string?>? settings = null)
    {
        return RunningServer.StartAsync(settings, services => services.AddSingleton(TestCommandRegistry.Create()));
    }

    private static async Task<string> CreateAsync(RunningServer server)
    {
        return (await server.Services.GetRequiredService<ProjectService>().CreateAsync(UserId.Local, "Song", CancellationToken.None)).Id;
    }

    // A large document, so that every change is a message too big for a client that is not reading to swallow in its buffers.
    private static async Task FillAsync(RunningServer server, string project, int markers)
    {
        ProjectSession session = await server.Services.GetRequiredService<OpenProjects>().OpenAsync(UserId.Local, ProjectId.From(project), CancellationToken.None);
        string name = new('n', 60);
        session.Update(document => document with
        {
            Markers = [.. Enumerable.Range(1, markers).Select(i => new Marker { Id = MarkerId.From($"mrk_{i}"), Time = i, Name = name })],
        });
    }

    private static string Json(string project, string members)
    {
        return "{\"project\":\"" + project + "\"" + (members.Length == 0 ? string.Empty : "," + members) + "}";
    }
}
