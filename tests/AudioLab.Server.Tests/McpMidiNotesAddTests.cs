using AudioLab.Core.Model;
using AudioLab.Core.Realtime;
using AudioLab.Core.Rules;
using AudioLab.Server.Mcp;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace AudioLab.Server.Tests;

public sealed class McpMidiNotesAddTests
{
    [Fact]
    public async Task AListOfTheMostNotesIsAddedInOneCallAndItsAnswerIsUnderTheResultLimit()
    {
        await using RunningServer server = await RunningServer.StartAsync();
        string project = await McpTestClient.CreateProjectAsync(server, UserId.Local);
        await using McpClient agent = await McpTestClient.ConnectAsync(server);
        string track = Assert.Single(McpTestClient.Reply(await McpTestClient.ApplyAsync(agent, "track_add", project, ("kind", "instrument"))).Created);
        string clip = Assert.Single(
            McpTestClient.Reply(await McpTestClient.ApplyAsync(agent, "midi_clip_add", project, ("track", track), ("start", 0), ("length", 600))).Created);
        object[] notes = [.. Enumerable.Range(0, NoteRules.MaxAddedNotes).Select(index => (object)new Dictionary<string, object> { ["pitch"] = 36 + (index % 48), ["start"] = index * 0.05 })];

        CallToolResult added = await McpTestClient.ApplyAsync(agent, "midi_notes_add", project, ("clip", clip), ("notes", notes));

        Assert.NotEqual(true, added.IsError);
        CommandReply reply = McpTestClient.Reply(added);
        Assert.True(reply.Succeeded && reply.Changed);
        Assert.Equal(NoteRules.MaxAddedNotes, reply.Created.Length);
        Assert.True(McpResults.Length(added) <= new McpOptions().MaxResultChars, $"The answer is {McpResults.Length(added)} characters.");
    }

    [Fact]
    public async Task OneNoteMoreThanTheMostIsRefusedBeforeAnythingChanges()
    {
        await using RunningServer server = await RunningServer.StartAsync();
        string project = await McpTestClient.CreateProjectAsync(server, UserId.Local);
        await using McpClient agent = await McpTestClient.ConnectAsync(server);
        string track = Assert.Single(McpTestClient.Reply(await McpTestClient.ApplyAsync(agent, "track_add", project, ("kind", "instrument"))).Created);
        string clip = Assert.Single(
            McpTestClient.Reply(await McpTestClient.ApplyAsync(agent, "midi_clip_add", project, ("track", track), ("start", 0), ("length", 600))).Created);
        object[] notes = [.. Enumerable.Range(0, NoteRules.MaxAddedNotes + 1).Select(index => (object)new Dictionary<string, object> { ["pitch"] = 60, ["start"] = index * 0.05 })];

        CallToolResult refused = await McpTestClient.ApplyAsync(agent, "midi_notes_add", project, ("clip", clip), ("notes", notes));

        Assert.True(refused.IsError);
        Assert.NotEmpty(McpTestClient.Reply(refused).Errors);
    }
}
