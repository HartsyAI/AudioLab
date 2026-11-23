using AudioLab.Core.Model;
using AudioLab.Core.Realtime;
using AudioLab.Core.Rules;
using AudioLab.Server.Mcp;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace AudioLab.Server.Tests;

public sealed class McpMidiNotesAddTests
{
    private static async Task<string> NewClipAsync(McpClient agent, string project)
    {
        CommandReply made = McpTestClient.Reply(await McpTestClient.ApplyAsync(agent, "track_add", project, ("kind", "instrument")));
        string track = Assert.Single(made.Created);
        CommandReply clip = McpTestClient.Reply(
            await McpTestClient.ApplyAsync(agent, "midi_clip_add", project, ("track", track), ("start", 0), ("length", 600)));
        return Assert.Single(clip.Created);
    }

    private static object[] Notes(int count)
    {
        return
        [
            .. Enumerable.Range(0, count)
                .Select(index => (object)new Dictionary<string, object> { ["pitch"] = 36 + (index % 48), ["start"] = index * 0.05 }),
        ];
    }

    [Fact]
    public async Task AListOfTheMostNotesIsAddedInOneCallAndItsAnswerIsUnderTheResultLimit()
    {
        await using RunningServer server = await RunningServer.StartAsync();
        string project = await McpTestClient.CreateProjectAsync(server, UserId.Local);
        await using McpClient agent = await McpTestClient.ConnectAsync(server);
        string clip = await NewClipAsync(agent, project);

        object[] notes = Notes(NoteRules.MaxAddedNotes);
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
        string clip = await NewClipAsync(agent, project);

        object[] notes = Notes(NoteRules.MaxAddedNotes + 1);
        CallToolResult refused = await McpTestClient.ApplyAsync(agent, "midi_notes_add", project, ("clip", clip), ("notes", notes));

        Assert.True(refused.IsError);
        Assert.NotEmpty(McpTestClient.Reply(refused).Errors);
    }
}
