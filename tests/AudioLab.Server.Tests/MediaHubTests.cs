using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AudioLab.Core.Commands;
using AudioLab.Core.Model;
using AudioLab.Server.Json;
using AudioLab.Server.Realtime;
using AudioLab.Server.Services;

namespace AudioLab.Server.Tests;

public sealed class MediaHubTests
{
    private const string Secret = "0123456789abcdef0123456789abcdef";

    private static async Task<MediaSummary> UploadAsync(RunningServer server, string user, byte[] file)
    {
        using ByteArrayContent content = new(file);
        content.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
        using HttpRequestMessage request = new(HttpMethod.Post, "/api/media?name=over-the-wire.wav") { Content = content };
        request.Headers.Add("X-AudioLab-Secret", Secret);
        request.Headers.Add("X-AudioLab-User", user);
        using HttpResponseMessage response = await server.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync(MediaJsonContext.Default.MediaSummary))!;
    }

    private static async Task<string> CreateProjectAsync(RunningServer server, string user)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, "/api/projects");
        request.Content = JsonContent.Create(new CreateProjectRequest("Over the wire"), ServerJsonContext.Default.CreateProjectRequest);
        request.Headers.Add("X-AudioLab-Secret", Secret);
        request.Headers.Add("X-AudioLab-User", user);
        using HttpResponseMessage response = await server.Client.SendAsync(request);
        return (await response.Content.ReadFromJsonAsync(ServerJsonContext.Default.ProjectSummary))!.Id;
    }

    [Fact]
    public async Task AClipAddThroughTheHubIsRefusedForAnotherUsersFileAndAWrongDurationAndAcceptedForTheUsersOwn()
    {
        await using RunningServer server = await RunningServer.StartAsync(new Dictionary<string, string?>
        {
            ["AudioLab:Door:Kind"] = "Swarm",
            ["AudioLab:Door:ProxySecret"] = Secret,
        });
        MediaSummary alices = await UploadAsync(server, "alice", MediaTestSupport.Wav());
        MediaSummary bobs = await UploadAsync(server, "bob", MediaTestSupport.Wav());
        string project = await CreateProjectAsync(server, "alice");

        await using HubClient alice = await HubClient.ConnectAsync(server, options =>
        {
            options.Headers["X-AudioLab-Secret"] = Secret;
            options.Headers["X-AudioLab-User"] = "alice";
        });
        await alice.JoinAsync(project);
        CommandReply track = await alice.ApplyAsync(project, "track_add", $"{{\"project\":\"{project}\",\"kind\":\"audio\"}}");
        string trackId = track.Created.Single();
        await alice.NextChangeAsync();
        string Add(MediaSummary media, double duration)
        {
            string seconds = duration.ToString("R", CultureInfo.InvariantCulture);
            return $"{{\"project\":\"{project}\",\"track\":\"{trackId}\",\"media\":\"{media.Id}\",\"duration\":{seconds}}}";
        }

        CommandReply foreign = await alice.ApplyAsync(project, "clip_add", Add(bobs, bobs.Duration));
        CommandReply missing = await alice.ApplyAsync(project, "clip_add", Add(bobs with { Id = "med_nothing" }, bobs.Duration));
        CommandReply wrongLength = await alice.ApplyAsync(project, "clip_add", Add(alices, alices.Duration + 1));
        Assert.False(foreign.Succeeded);
        Assert.Equal(CommandErrorCode.NotFound, foreign.Errors.Single().Code);
        Assert.Equal(missing.Errors, foreign.Errors);
        Assert.Equal(CommandErrorCode.Rejected, wrongLength.Errors.Single().Code);
        Assert.Equal(track.Revision, foreign.Revision);
        Assert.True(await alice.HeardNothingAsync());

        CommandReply own = await alice.ApplyAsync(project, "clip_add", Add(alices, alices.Duration));
        Assert.True(own.Succeeded);
        Assert.Equal(track.Revision + 1, own.Revision);
    }
}
