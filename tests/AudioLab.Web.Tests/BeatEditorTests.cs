using AudioLab.Core.Commands;
using AudioLab.Core.Model;
using AudioLab.Web.Services;

namespace AudioLab.Web.Tests;

/// <summary>The drum pattern edits: a refusal from the server comes back as its own words, and nothing is claimed as applied.</summary>
public sealed class BeatEditorTests
{
    private readonly FakeProjectHub _hub = new()
    {
        JoinDocument = ProjectJson.Write(Song.Empty().With(Song.NewTrack("t1"))),
    };

    private async Task<BeatEditor> OpenAsync()
    {
        LiveProject live = new(new FakeHubFactory(_hub), new CommandSender(CoreCommands.CreateRegistry()));
        await live.OpenAsync("song");
        _hub.Calls.Clear();
        return new BeatEditor(live);
    }

    /// <summary>A lane the server does not have is refused with the server's own words, and the edit is not applied.</summary>
    [Fact]
    public async Task ARefusedRenameShowsTheServersWords()
    {
        BeatEditor editor = await OpenAsync();
        _hub.Reply = FakeProjectHub.Refused(CommandErrorCode.NotFound, "There is no drum lane with the id bln_gone.");

        EditResult result = await editor.RenameLaneAsync(BeatLaneId.From("bln_gone"), "Kick");

        Assert.False(result.Succeeded);
        Assert.Equal("There is no drum lane with the id bln_gone.", result.Refusal);
    }

    /// <summary>A swing the server refuses is shown in its words too, not replaced by the client's guess.</summary>
    [Fact]
    public async Task ARefusedSwingShowsTheServersWords()
    {
        BeatEditor editor = await OpenAsync();
        _hub.Reply = FakeProjectHub.Refused(CommandErrorCode.Invalid, "swing must be from 0 to 1.");

        EditResult result = await editor.SetSwingAsync(0.5);

        Assert.False(result.Succeeded);
        Assert.Equal("Swing must be from 0 to 1.", result.Refusal);
    }
}
