using AudioLab.Core.Api;
using AudioLab.Core.Commands;
using AudioLab.Core.Model;
using AudioLab.Core.Realtime;
using AudioLab.Server.Services;
using AudioLab.Web.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AudioLab.Server.Tests;

/// <summary>The UI's own hub client against the real server, with no negotiation, as the page connects.</summary>
public sealed class WebHubClientTests
{
    private static async Task<ProjectSummary> CreateAsync(RunningServer server)
    {
        return await server.Services.GetRequiredService<ProjectService>().CreateAsync(UserId.Local, "Song", CancellationToken.None);
    }

    private static ProjectRenamer Renamer(RunningServer server)
    {
        SignalRProjectHubFactory hubs = new(new Uri(server.Client.BaseAddress!, server.HubPath));
        return new ProjectRenamer(hubs, new CommandSender(CoreCommands.CreateRegistry()));
    }

    [Fact]
    public async Task ARenameFromTheListReachesEveryClientOfTheProjectAndTheListing()
    {
        await using RunningServer server = await RunningServer.StartAsync();
        ProjectSummary project = await CreateAsync(server);
        await using HubClient watcher = await HubClient.ConnectAsync(server);
        await watcher.JoinAsync(project.Id);

        CommandReply reply = await Renamer(server).RenameAsync(project.Id, "Night drive");

        Assert.True(reply.Succeeded);
        ProjectChange heard = await watcher.NextChangeAsync();
        Assert.Equal("project_rename", heard.Reason);
        Assert.Equal("Night drive", ProjectJson.Read(heard.Document).Name);
        ProjectListing listing = await server.Services.GetRequiredService<ProjectService>().ListAsync(UserId.Local, CancellationToken.None);
        Assert.Equal("Night drive", listing.Projects.Single().Name);
    }

    [Fact]
    public async Task ABlankNameIsRefusedByTheServerWithItsReasonAndNothingChanges()
    {
        await using RunningServer server = await RunningServer.StartAsync();
        ProjectSummary project = await CreateAsync(server);

        CommandReply reply = await Renamer(server).RenameAsync(project.Id, "   ");

        Assert.False(reply.Succeeded);
        Assert.Equal(CommandErrorCode.Rejected, reply.Errors.Single().Code);
        Assert.False(string.IsNullOrWhiteSpace(CommandRefusal.Describe(reply)));
        ProjectListing listing = await server.Services.GetRequiredService<ProjectService>().ListAsync(UserId.Local, CancellationToken.None);
        Assert.Equal("Song", listing.Projects.Single().Name);
    }

    [Fact]
    public async Task TheClientConnectsWithoutNegotiationJoinsAndReportsItsState()
    {
        await using RunningServer server = await RunningServer.StartAsync();
        ProjectSummary project = await CreateAsync(server);
        SignalRProjectHub hub = new(new Uri(server.Client.BaseAddress!, server.HubPath), reconnects: false);
        await using (hub)
        {
            await hub.ConnectAsync();
            JoinReply joined = await hub.JoinAsync(project.Id);
            Assert.Equal(project.Id, joined.Project);
            Assert.Equal(HubState.Connected, hub.State);
        }
    }
}
