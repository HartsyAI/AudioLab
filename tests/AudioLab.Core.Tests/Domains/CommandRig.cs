using AudioLab.Core.Commands;
using AudioLab.Core.Model;

namespace AudioLab.Core.Tests;

/// <summary>A history over the real registry, with helpers that state what a test expects of a command.</summary>
internal sealed class CommandRig
{
    private static readonly ClientId _client = ClientId.From("test-client");

    // A rig made from a document that another rig edited starts its ids higher, so they never repeat.
    public CommandRig(ProjectDocument? start = null, int idStart = 1000)
    {
        History = new CommandHistory(start ?? BlankProject.Create(), CoreCommands.CreateRegistry(), new SequentialIdService(idStart));
    }

    public CommandHistory History { get; }

    public ProjectDocument Project => History.Current;

    public ProjectId Id => History.Current.Id;

    public CommandResult Run(Command command, GestureId? gesture = null) => History.Apply(command, _client, gesture);

    // The command must succeed; returns the document after it.
    public ProjectDocument Ok(Command command)
    {
        CommandResult result = Run(command);
        Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(error => error.Message)));
        return result.Document!;
    }

    // The command must succeed and report the ids it allocated.
    public IReadOnlyList<string> Created(Command command)
    {
        CommandResult result = Run(command);
        Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(error => error.Message)));
        return result.Created;
    }

    // The command must be refused and leave the document and the history as they were.
    public CommandResult Refused(Command command)
    {
        ProjectDocument before = Project;
        int undo = History.UndoCount;
        CommandResult result = Run(command);
        Assert.False(result.Succeeded);
        Assert.NotEmpty(result.Errors);
        Assert.Equal(before, Project);
        Assert.Equal(undo, History.UndoCount);
        return result;
    }

    public void RefusedWith(Command command, CommandErrorCode code)
    {
        Assert.Equal(code, Refused(command).Errors[0].Code);
    }
}
