using AudioLab.Core.Commands;
using AudioLab.Core.Model;
using AudioLab.Web.Services;

namespace AudioLab.Web.Tests;

/// <summary>What a group's edits send: a volume is one gesture, so the send carries its id and the gesture ends with it.</summary>
public sealed class GroupEditorTests
{
    /// <summary>A group's volume is sent once, inside a gesture, and the gesture is ended after the send.</summary>
    [Fact]
    public async Task AGroupVolumeIsSentOnceInsideAGestureThatEnds()
    {
        FakeProjectHub hub = new();
        LiveProject live = new(new FakeHubFactory(hub), new CommandSender(CoreCommands.CreateRegistry()));
        await live.OpenAsync("song");
        GroupEditor editor = new(live);

        EditResult result = await editor.SetVolumeAsync(GroupId.From("grp_drums"), 0.5);

        Assert.True(result.Succeeded);
        (string Command, string Json, string? Gesture) sent = Assert.Single(hub.Sent);
        Assert.False(string.IsNullOrEmpty(sent.Gesture));
        string ended = Assert.Single(hub.Calls, call => call.StartsWith("end ", StringComparison.Ordinal));
        Assert.EndsWith(" " + sent.Gesture, ended, StringComparison.Ordinal);
        await live.DisposeAsync();
    }
}
