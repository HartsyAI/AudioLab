using AudioLab.Core.Commands;
using AudioLab.Core.Model;

namespace AudioLab.Web.Session.Roll;

/// <summary>What the user did in the piano roll, as commands to send in one gesture, so it is one undo step.</summary>
/// <param name="Build">The commands for a project, in the order they are sent.</param>
/// <param name="SelectCreated">Whether the notes the commands make become the selection.</param>
public sealed record RollEdit(Func<ProjectId, IReadOnlyList<Command>> Build, bool SelectCreated = false)
{
    /// <summary>An edit of one command.</summary>
    public static RollEdit Of(Func<ProjectId, Command> command, bool selectCreated = false)
    {
        ArgumentNullException.ThrowIfNull(command);
        return new RollEdit(project => [command(project)], selectCreated);
    }
}
