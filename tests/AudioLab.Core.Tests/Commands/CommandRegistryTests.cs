using AudioLab.Core.Commands;
using AudioLab.Core.Model;

namespace AudioLab.Core.Tests;

public sealed class CommandRegistryTests
{
    private readonly CommandRegistry _registry = TestRegistry.Create();

    private readonly ProjectDocument _project = TestRegistry.Project();

    [Fact]
    public void AHandlerMintsIdsOnlyFromTheIdService()
    {
        AddMarkerCommand command = new() { Project = _project.Id, Time = 40, Name = "Chorus" };

        CommandResult result = _registry.Execute(_project, command, TestRegistry.Ids());

        Assert.True(result.Succeeded);
        Assert.Equal(["mrk_1001"], result.Created);
        Assert.Equal("mrk_1001", result.Document!.Markers[^1].Id.Value);
    }

    [Fact]
    public void TheSameCommandAndIdServiceGiveTheSameDocument()
    {
        AddMarkerCommand command = new() { Project = _project.Id, Time = 40, Name = "Chorus" };

        CommandResult first = _registry.Execute(_project, command, TestRegistry.Ids());
        CommandResult second = _registry.Execute(_project, command, TestRegistry.Ids());

        Assert.Equal(first, second);
    }

    [Fact]
    public void ACommandForAnotherProjectIsRefusedAndNothingChanges()
    {
        ProjectDocument other = _project with { Id = ProjectId.From("another-project") };
        RenameProjectCommand command = new() { Project = other.Id, Name = "Stolen" };

        CommandResult result = _registry.Execute(_project, command, TestRegistry.Ids());

        Assert.False(result.Succeeded);
        Assert.Equal(CommandErrorCode.WrongProject, Assert.Single(result.Errors).Code);
    }

    [Fact]
    public void ACommandWithNoHandlerIsRefused()
    {
        UnregisteredCommand command = new() { Project = _project.Id };

        CommandResult result = _registry.Execute(_project, command, TestRegistry.Ids());

        Assert.Equal(CommandErrorCode.UnknownCommand, Assert.Single(result.Errors).Code);
    }

    [Fact]
    public void AHandlerRefusalIsReturnedAsItIs()
    {
        CommandResult result = _registry.Execute(_project, new RefusedCommand { Project = _project.Id }, TestRegistry.Ids());

        Assert.Null(result.Document);
        Assert.Equal(new CommandError(CommandErrorCode.NotFound, "There is nothing to refuse."), Assert.Single(result.Errors));
    }

    [Fact]
    public void AnEditThatBreaksTheProjectRulesIsRefusedWithTheValidatorsList()
    {
        CommandResult result = _registry.Execute(
            _project,
            new SetBpmCommand { Project = _project.Id, Bpm = 5000 },
            TestRegistry.Ids());

        Assert.False(result.Succeeded);
        CommandError error = Assert.Single(result.Errors);
        Assert.Equal(CommandErrorCode.Invalid, error.Code);
        Assert.StartsWith("bpm:", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EveryProblemIsListedUpToTheCapThenCounted()
    {
        CommandResult few = _registry.Execute(_project, new BreakCommand { Project = _project.Id, Count = 3 }, TestRegistry.Ids());
        CommandResult many = _registry.Execute(_project, new BreakCommand { Project = _project.Id, Count = 150 }, TestRegistry.Ids());

        Assert.Equal(3, few.Errors.Count);
        Assert.All(few.Errors, error => Assert.Equal(CommandErrorCode.Invalid, error.Code));
        Assert.Equal(ProjectLimits.MaxProblems + 1, many.Errors.Count);
        Assert.Equal("and 50 more.", many.Errors[^1].Message);
    }

    [Fact]
    public void ACommandCannotChangeWhoOwnsTheProject()
    {
        HijackCommand command = new() { Project = _project.Id, NewOwner = UserId.ForDoor("hosted", "someone") };

        CommandResult result = _registry.Execute(_project, command, TestRegistry.Ids());

        Assert.Equal(CommandErrorCode.Invalid, Assert.Single(result.Errors).Code);
    }

    [Fact]
    public void ACommandThatChangesNothingSucceedsWithTheSameDocument()
    {
        CommandResult result = _registry.Execute(_project, new NothingCommand { Project = _project.Id }, TestRegistry.Ids());

        Assert.True(result.Succeeded);
        Assert.Equal(_project, result.Document);
    }

    [Fact]
    public void NamesAreListedInOrderWithTheirTypesAndWords()
    {
        IReadOnlyList<CommandDescriptor> commands = _registry.Commands;

        Assert.Equal(
            ["add_marker", "break", "hijack", "nothing", "refused", "rename_project", "set_bpm", "shape"],
            commands.Select(command => command.Name));
        CommandDescriptor marker = commands[0];
        Assert.Equal(typeof(AddMarkerCommand), marker.CommandType);
        Assert.Equal("Adds a marker and allocates its id.", marker.Description);
        Assert.Same(marker, _registry.Find("add_marker"));
        Assert.Null(_registry.Find("missing"));
        Assert.Equal("add_marker", _registry.NameOf(new AddMarkerCommand { Project = _project.Id, Time = 0, Name = "x" }));
        Assert.Null(_registry.NameOf(new UnregisteredCommand { Project = _project.Id }));
    }

    [Fact]
    public void RegisteringTheSameNameOrTypeTwiceFails()
    {
        TestCommandJsonContext json = TestCommandJsonContext.Default;
        CommandRegistryBuilder builder = new CommandRegistryBuilder()
            .Add("nothing", "Changes nothing.", json.NothingCommand, (document, _, _) => CommandResult.Ok(document));

        Assert.Throws<InvalidOperationException>(() => builder.Add(
            "nothing", "Again.", json.RefusedCommand, (document, _, _) => CommandResult.Ok(document)));
        Assert.Throws<InvalidOperationException>(() => builder.Add(
            "also_nothing", "Again.", json.NothingCommand, (document, _, _) => CommandResult.Ok(document)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Track_rename")]
    [InlineData("track.rename")]
    [InlineData("track-rename")]
    [InlineData("1track")]
    [InlineData("_track")]
    public void AWireNameThatAnMcpToolCouldNotCarryIsRefused(string name)
    {
        TestCommandJsonContext json = TestCommandJsonContext.Default;

        Assert.Throws<ArgumentException>(() => new CommandRegistryBuilder()
            .Add(name, "Changes nothing.", json.NothingCommand, (document, _, _) => CommandResult.Ok(document)));
    }

    [Fact]
    public void ACommandTypeThatCouldBeSubclassedIsRefused()
    {
        TestCommandJsonContext json = TestCommandJsonContext.Default;

        Assert.Throws<ArgumentException>(() => new CommandRegistryBuilder()
            .Add("unsealed", "Is not sealed.", json.UnsealedCommand, (document, _, _) => CommandResult.Ok(document)));
    }

    [Fact]
    public void AJsonContextThatIgnoresUnknownMembersIsRefused()
    {
        Assert.Throws<ArgumentException>(() => new CommandRegistryBuilder()
            .Add("nothing", "Changes nothing.", LaxCommandJsonContext.Default.NothingCommand, (document, _, _) => CommandResult.Ok(document)));
    }

    [Fact]
    public void ARefusalAlwaysSaysWhy()
    {
        Assert.Throws<ArgumentException>(() => CommandResult.Refuse([]));
    }
}
