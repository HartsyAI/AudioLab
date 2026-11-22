using AudioLab.Core.Commands;
using AudioLab.Core.Model;
using AudioLab.Core.Realtime;
using AudioLab.Web.Services;

namespace AudioLab.Web.Tests;

/// <summary>Holds the rename and send tests.</summary>
public sealed class RenameAndSendTests
{
    private readonly FakeProjectHub _hub = new();

    private readonly CommandSender _sender = new(CoreCommands.CreateRegistry());

    /// <summary>A rename joins sends the registrys wire name leaves and closes a connection that does not reconnect.</summary>
    [Fact]
    public async Task ARenameJoinsSendsTheRegistrysWireNameLeavesAndClosesAConnectionThatDoesNotReconnect()
    {
        FakeHubFactory factory = new(_hub);

        CommandReply reply = await new ProjectRenamer(factory, _sender).RenameAsync("song-1", "Night Drive");

        Assert.True(reply.Succeeded);
        Assert.Equal(["connect", "join song-1"], _hub.Calls.Take(2));
        string[] apply = _hub.Calls[2].Split(' ', 4);
        Assert.Equal(["apply", "song-1", "project_rename"], apply[..3]);
        RenameProject sent = Assert.IsType<RenameProject>(CoreCommands.CreateRegistry().Parse("project_rename", apply[3]).Command);
        Assert.Equal("Night Drive", sent.Name);
        Assert.Equal(["leave song-1", "dispose"], _hub.Calls.TakeLast(2));
        Assert.False(factory.Reconnects);
    }

    /// <summary>A refusal is handed back not thrown and the connection is still closed.</summary>
    [Fact]
    public async Task ARefusalIsHandedBackNotThrownAndTheConnectionIsStillClosed()
    {
        _hub.Reply = FakeProjectHub.Refused(CommandErrorCode.Rejected, "a project name may not be blank");

        CommandReply reply = await new ProjectRenamer(new FakeHubFactory(_hub), _sender).RenameAsync("song-1", " ");

        Assert.False(reply.Succeeded);
        Assert.True(_hub.Disposed);
    }

    /// <summary>A command that is not registered is a bug and is never sent.</summary>
    [Fact]
    public async Task ACommandThatIsNotRegisteredIsABugAndIsNeverSent()
    {
        CommandSender empty = new(new CommandRegistryBuilder().Build());
        RenameProject command = new() { Project = ProjectId.From("song-1"), Name = "x" };

        await Assert.ThrowsAsync<InvalidOperationException>(() => empty.SendAsync(_hub, command));

        Assert.Empty(_hub.Calls);
    }

    /// <summary>Refusal reasons are shown as the server gave them as one sentence.</summary>
    [Fact]
    public void RefusalReasonsAreShownAsTheServerGaveThemAsOneSentence()
    {
        CommandReply two = new(
            false,
            false,
            3,
            [
                new HubCommandError(CommandErrorCode.Invalid, "start: must be 0 or more"),
                new HubCommandError(CommandErrorCode.Invalid, "track: unknown."),
            ],
            []);

        Assert.Equal("Start: must be 0 or more; track: unknown.", CommandRefusal.Describe(two));
        Assert.Equal("The server refused the edit.", CommandRefusal.Describe(new CommandReply(false, false, 3, [], [])));
    }
}
