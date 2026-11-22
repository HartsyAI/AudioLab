using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AudioLab.Core.Commands;
using AudioLab.Core.Model;
using AudioLab.Server.Json;
using AudioLab.Server.Realtime;
using AudioLab.Server.Services;
using Microsoft.AspNetCore.SignalR;

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

    [Fact]
    public async Task AProjectThatNamesMissingMediaIsRefusedOnJoinWithTheRealReasonAndTheProjectsOwnIds()
    {
        using TempFolder folder = new();
        string root = folder.Path;
        string project;
        await using (RunningServer first = await RunningServer.StartAsync(new Dictionary<string, string?> { ["AudioLab:Storage:Root"] = root }))
        {
            using HttpResponseMessage made = await first.Client.PostAsJsonAsync(
                "/api/projects",
                new CreateProjectRequest("Lost"),
                ServerJsonContext.Default.CreateProjectRequest);
            project = (await made.Content.ReadFromJsonAsync(ServerJsonContext.Default.ProjectSummary))!.Id;
            using ByteArrayContent wav = new(MediaTestSupport.Wav());
            wav.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
            using HttpResponseMessage uploaded = await first.Client.PostAsync("/api/media?name=m.wav", wav);
            MediaSummary media = (await uploaded.Content.ReadFromJsonAsync(MediaJsonContext.Default.MediaSummary))!;
            await using HubClient editor = await HubClient.ConnectAsync(first);
            await editor.JoinAsync(project);
            string track = (await editor.ApplyAsync(project, "track_add", $"{{\"project\":\"{project}\",\"kind\":\"audio\"}}")).Created.Single();
            string seconds = media.Duration.ToString("R", CultureInfo.InvariantCulture);
            string add = $"{{\"project\":\"{project}\",\"track\":\"{track}\",\"media\":\"{media.Id}\",\"duration\":{seconds}}}";
            Assert.True((await editor.ApplyAsync(project, "clip_add", add)).Succeeded);
            using HttpResponseMessage saved = await first.Client.PostAsync($"/api/projects/{project}/save", null);
            Assert.True(saved.IsSuccessStatusCode);
        }
        foreach (string file in Directory.GetFiles(root, "m*", SearchOption.AllDirectories))
        {
            File.Delete(file);
        }

        await using RunningServer second = await RunningServer.StartAsync(new Dictionary<string, string?> { ["AudioLab:Storage:Root"] = root });
        await using HubClient joiner = await HubClient.ConnectAsync(second);
        HubException refused = await Assert.ThrowsAsync<HubException>(() => joiner.JoinAsync(project));
        Assert.Contains("media", refused.Message, StringComparison.Ordinal);
        Assert.Contains("no such media file", refused.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("The project file is not valid.", refused.Message, StringComparison.Ordinal);
        using HttpResponseMessage opened = await second.Client.GetAsync($"/api/projects/{project}");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, opened.StatusCode);
        Assert.Contains("media files it names", await opened.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }
}
