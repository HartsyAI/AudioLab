using AudioLab.Core.Commands;
using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Web.Session.Roll;

/// <summary>What the user did in the piano roll, as commands to send in one gesture, so it is one undo step.</summary>
/// <param name="Build">The commands for a project, in the order they are sent.</param>
/// <param name="SelectCreated">Whether the notes the commands make become the selection.</param>
public sealed record RollEdit(Func<ProjectId, IReadOnlyList<Command>> Build, bool SelectCreated = false)
{
    /// <summary>The most items of one list a command of the roll carries, so a command stays far under the hub's 2 Mi characters.</summary>
    public const int ChunkSize = NoteRules.MaxAddedNotes;

    /// <summary>An edit of one command.</summary>
    public static RollEdit Of(Func<ProjectId, Command> command, bool selectCreated = false)
    {
        ArgumentNullException.ThrowIfNull(command);
        return new RollEdit(project => [command(project)], selectCreated);
    }

    /// <summary>An edit of one command for each <see cref="ChunkSize"/> items, sent in one gesture.</summary>
    public static RollEdit Chunked<T>(IReadOnlyList<T> items, Func<ProjectId, ValueList<T>, Command> command, bool selectCreated = false)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(command);
        T[][] parts = [.. items.Chunk(ChunkSize)];
        return new RollEdit(project => [.. parts.Select(part => command(project, [.. part]))], selectCreated);
    }
}
