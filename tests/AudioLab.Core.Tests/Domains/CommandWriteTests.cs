using AudioLab.Core.Commands;
using AudioLab.Core.Model;

namespace AudioLab.Core.Tests;

public sealed class CommandWriteTests
{
    private readonly CommandRegistry _registry = CoreCommands.CreateRegistry();

    [Fact]
    public void AWrittenCommandIsReadBackAsTheSameCommandUnderItsWireName()
    {
        MoveClip command = new() { Project = ProjectId.From("song"), Clip = ClipId.From("c1"), Start = 12.5, Track = TrackId.From("t2") };

        Assert.True(_registry.TryWrite(command, out string? name, out string? json));

        Assert.Equal("clip_move", name);
        CommandParseResult parsed = _registry.Parse(name, json);
        Assert.Equal(command, parsed.Command);
    }

    [Fact]
    public void ACommandThatIsNotRegisteredIsNotWritten()
    {
        Assert.False(_registry.TryWrite(new UnregisteredCommand { Project = ProjectId.From("song") }, out string? name, out string? json));
        Assert.Null(name);
        Assert.Null(json);
    }

    private sealed record UnregisteredCommand : Command;
}
