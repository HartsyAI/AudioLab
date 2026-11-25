using AudioLab.Core.Commands;
using AudioLab.Web.Services;

namespace AudioLab.Web.Tests;

/// <summary>What the master fader sends: a drag is one gesture, so the send carries its id and the gesture ends with it.</summary>
public sealed class MasterEditorTests
{
    /// <summary>The master's gain is sent once, inside a gesture, and the gesture is ended after the send.</summary>
    [Fact]
    public async Task AMasterGainIsSentOnceInsideAGestureThatEnds()
    {
        FakeProjectHub hub = new();
        LiveProject live = new(new FakeHubFactory(hub), new CommandSender(CoreCommands.CreateRegistry()));
        await live.OpenAsync("song");
        MasterEditor editor = new(live);

        EditResult result = await editor.SetVolumeAsync(0.5);

        Assert.True(result.Succeeded);
        (string Command, string Json, string? Gesture) sent = Assert.Single(hub.Sent);
        Assert.False(string.IsNullOrEmpty(sent.Gesture));
        string ended = Assert.Single(hub.Calls, call => call.StartsWith("end ", StringComparison.Ordinal));
        Assert.EndsWith(" " + sent.Gesture, ended, StringComparison.Ordinal);
        await live.DisposeAsync();
    }
}
