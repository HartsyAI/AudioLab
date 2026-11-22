using System.Text.Json;
using AudioLab.Core.Commands;
using AudioLab.Core.Model;

namespace AudioLab.Core.Tests;

public sealed class CommandParseTests
{
    private readonly CommandRegistry _registry = TestRegistry.Create();

    [Fact]
    public void ACommandReadsFromItsOwnJson()
    {
        CommandParseResult result = _registry.Parse("add_marker", """{"project":"p-1","time":12.5,"name":"Drop"}""");

        Assert.True(result.Succeeded);
        AddMarkerCommand command = Assert.IsType<AddMarkerCommand>(result.Command);
        Assert.Equal(12.5, command.Time);
        Assert.Equal("p-1", command.Project.Value);
    }

    [Theory]
    [InlineData("""{"project":"p-1","time":1,"name":"x","id":"mrk_chosen"}""")]
    [InlineData("""{"project":"p-1","time":1}""")]
    [InlineData("""{"project":"p-1","time":1,"name":null}""")]
    [InlineData("""{"project":"p-1","time":"1","name":"x"}""")]
    [InlineData("""{"project":"P-1","time":1,"name":"x"}""")]
    [InlineData("""{"project":"p-1","time":1,"time":2,"name":"x"}""")]
    [InlineData("""{"project":"p-1","time":1,"name":"x"} trailing""")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("")]
    [InlineData("{")]
    public void TextThatIsNotThatCommandIsRefusedNotThrown(string json)
    {
        CommandParseResult result = _registry.Parse("add_marker", json);

        Assert.False(result.Succeeded);
        CommandError error = Assert.Single(result.Errors);
        Assert.Equal(CommandErrorCode.Malformed, error.Code);
        Assert.InRange(error.Message.Length, 1, ProjectLimits.MaxProblemLength + 3);
    }

    [Fact]
    public void AClientCannotChooseTheIdOfWhatItCreates()
    {
        CommandParseResult result = _registry.Parse("add_marker", """{"project":"p-1","time":1,"name":"x","id":"mrk_chosen"}""");

        Assert.Contains("id", Assert.Single(result.Errors).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnUnknownCommandNameIsRefusedAndOnlyAWellFormedOneIsEchoed()
    {
        CommandParseResult named = _registry.Parse("delete_everything", "{}");
        CommandParseResult odd = _registry.Parse("<script>alert(1)</script>", "{}");

        Assert.Equal(CommandErrorCode.UnknownCommand, Assert.Single(named.Errors).Code);
        Assert.Contains("delete_everything", named.Errors[0].Message, StringComparison.Ordinal);
        Assert.Equal(CommandErrorCode.UnknownCommand, Assert.Single(odd.Errors).Code);
        Assert.DoesNotContain("script", odd.Errors[0].Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnOversizedCommandIsRefusedBeforeItIsRead()
    {
        string json = "{\"project\":\"p-1\",\"time\":1,\"name\":\"" + new string('x', ProjectLimits.MaxCommandLength) + "\"}";

        CommandParseResult result = _registry.Parse("add_marker", json);

        Assert.Equal(CommandErrorCode.Malformed, Assert.Single(result.Errors).Code);
        Assert.Contains("longer than", result.Errors[0].Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ACommandWrittenAndReadBackIsEqual()
    {
        ShapeCommand shape = new()
        {
            Project = TestRegistry.Project().Id,
            Track = null,
            Tracks = [TrackId.From("trk_1"), TrackId.From("trk_2")],
            Kind = TrackKind.Bus,
            Mode = TrackKind.Audio,
            Level = 0.5,
            Flags = [true, false],
            Labels = ["a", "b"],
        };
        string json = JsonSerializer.Serialize(shape, TestCommandJsonContext.Default.ShapeCommand);

        CommandParseResult result = _registry.Parse("shape", json);

        Assert.Equal(shape, result.Command);
    }
}
