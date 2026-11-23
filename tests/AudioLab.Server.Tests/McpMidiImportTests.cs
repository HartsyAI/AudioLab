using AudioLab.Core.Model;
using AudioLab.Core.Realtime;
using AudioLab.Server.Services;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using static AudioLab.Server.Tests.McpTestClient;

namespace AudioLab.Server.Tests;

public sealed class McpMidiImportTests
{
    [Fact]
    public async Task AnAgentImportsASmallClipAsAToolAndThereIsNoToolToExportAFile()
    {
        await using RunningServer server = await RunningServer.StartAsync();
        string project = await CreateProjectAsync(server, UserId.Local);
        await using McpClient client = await ConnectAsync(server);
        IList<McpClientTool> tools = await client.ListToolsAsync();
        CommandReply track = Reply(await ApplyAsync(client, "track_add", project, ("kind", "instrument")));

        CallToolResult result = await ApplyAsync(
            client,
            "midi_clip_import",
            project,
            ("track", track.Created.Single()),
            ("start", 1.0),
            ("length", 2.0),
            ("notes", new object[] { new { pitch = 60, start = 0.0, duration = 0.5, velocity = 100 } }),
            ("controlChanges", new object[] { new { time = 0.0, controller = 64, value = 127 } }));

        Assert.Contains(tools, tool => tool.Name == "midi_clip_import");
        Assert.DoesNotContain(tools, tool => tool.Name.Contains("export", StringComparison.OrdinalIgnoreCase));
        CommandReply reply = Reply(result);
        Assert.True(reply.Succeeded);
        ProjectService projects = server.Services.GetRequiredService<ProjectService>();
        ProjectDocument document = (await projects.OpenAsync(UserId.Local, ProjectId.From(project), CancellationToken.None)).Document;
        MidiClip clip = Assert.Single(document.Tracks.Single().MidiClips);
        Assert.Equal(reply.Created.Single(), clip.Id.Value);
        Assert.Equal([60], clip.Notes.Select(note => note.Pitch));
        Assert.Equal([new MidiControlChange(0, 64, 127)], clip.ControlChanges);
    }

    [Fact]
    public async Task AnImportThatBreaksTheClipsRulesIsARefusalAndChangesNothing()
    {
        await using RunningServer server = await RunningServer.StartAsync();
        string project = await CreateProjectAsync(server, UserId.Local);
        await using McpClient client = await ConnectAsync(server);
        CommandReply track = Reply(await ApplyAsync(client, "track_add", project, ("kind", "instrument")));

        CallToolResult result = await ApplyAsync(
            client,
            "midi_clip_import",
            project,
            ("track", track.Created.Single()),
            ("start", 0.0),
            ("length", 1.0),
            ("notes", new object[] { new { pitch = 200, start = 0.0, duration = 0.5, velocity = 100 } }),
            ("controlChanges", Array.Empty<object>()));

        Assert.True(result.IsError);
        Assert.False(Reply(result).Succeeded);
    }
}
