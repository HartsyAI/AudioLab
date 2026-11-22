using System.Text.Json;
using AudioLab.Core.Commands;
using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Core.Tests;

public sealed class CoreCommandsTests
{
    private readonly CommandRegistry _registry = CoreCommands.CreateRegistry();

    [Fact]
    public void EveryCommandRecordOfTheAssemblyIsRegisteredOnce()
    {
        HashSet<Type> declared =
        [
            .. typeof(Command).Assembly.GetTypes().Where(type => type is { IsAbstract: false, IsPublic: true } && type.IsSubclassOf(typeof(Command))),
        ];

        Assert.Equal(declared.Count, _registry.Commands.Count);
        Assert.Equal(declared, _registry.Commands.Select(command => command.CommandType).ToHashSet());
    }

    [Fact]
    public void EveryCommandNamesItsProjectAndHasAUsableSchema()
    {
        foreach (CommandDescriptor command in _registry.Commands)
        {
            Assert.True(CommandRegistryBuilder.IsValidName(command.Name), command.Name);
            Assert.Equal("object", command.Schema.GetProperty("type").GetString());
            Assert.False(command.Schema.GetProperty("additionalProperties").GetBoolean(), command.Name);
            string[] required = [.. command.Schema.GetProperty("required").EnumerateArray().Select(item => item.GetString()!)];
            Assert.Contains("project", required);
        }
    }

    [Fact]
    public void NoMemberOfAnyCommandSchemaIsEmpty()
    {
        List<string> empty = [];

        foreach (CommandDescriptor command in _registry.Commands)
        {
            SchemaWalk.Empty(command.Schema, command.Name, empty);
        }

        Assert.Empty(empty);
    }

    [Fact]
    public void TheRenamedSectionCommandsExistUnderTheirNewNamesOnly()
    {
        Assert.NotNull(_registry.Find("section_clear"));
        Assert.NotNull(_registry.Find("section_from_markers"));
        Assert.Null(_registry.Find("sections_clear"));
        Assert.Null(_registry.Find("sections_from_markers"));
        Assert.Equal(CommandErrorCode.UnknownCommand, _registry.Parse("sections_clear", "{}").Errors[0].Code);
    }

    [Fact]
    public void TheDomainsShipTheirCommandsUnderStableNames()
    {
        Assert.Contains(_registry.Commands, command => command.Name == "tempo_change_add");
        Assert.Contains(_registry.Commands, command => command.Name == "marker_add");
        Assert.Contains(_registry.Commands, command => command.Name == "section_set_span");
    }

    [Fact]
    public void ACommandRoundTripsThroughItsJsonAndRunsAgainstAProject()
    {
        ProjectDocument project = BlankProject.Create();
        string json = $$"""{"project":"{{project.Id.Value}}","section":"sec_none","start":1,"end":2,"edit":"move"}""";

        CommandParseResult parsed = _registry.Parse("section_set_span", json);

        SetSectionSpan command = Assert.IsType<SetSectionSpan>(parsed.Command);
        Assert.Equal(SectionEdit.Move, command.Edit);
        CommandResult result = _registry.Execute(project, command, new SequentialIdService());
        Assert.Equal(CommandErrorCode.NotFound, Assert.Single(result.Errors).Code);
    }

    [Theory]
    [InlineData("""{"project":"p","time":1,"name":"x","id":"mrk_chosen"}""")]
    [InlineData("""{"project":"p","edit":"sideways"}""")]
    [InlineData("""{"project":"P"}""")]
    public void AClientCannotChooseIdsOrSendLooseValues(string json)
    {
        Assert.False(_registry.Parse("marker_add", json).Succeeded);
        Assert.False(_registry.Parse("section_set_span", json).Succeeded);
    }

    [Fact]
    public void ARegistryBuiltTwiceIsEquivalent()
    {
        Assert.Equal(_registry.Commands.Select(command => command.Name), CoreCommands.CreateRegistry().Commands.Select(command => command.Name));
        Assert.Equal(
            JsonSerializer.Serialize(_registry.Commands.Select(command => command.Schema)),
            JsonSerializer.Serialize(CoreCommands.CreateRegistry().Commands.Select(command => command.Schema)));
    }
}
