using AudioLab.Core.Model;
using AudioLab.Server.Store;

namespace AudioLab.Server.Services;

/// <summary>Tells the clients of a project what happened to it. The hub implements it; a test can record it.</summary>
public interface IProjectEvents
{
    /// <summary>The project has a new revision.</summary>
    /// <param name="user">The owner.</param>
    /// <param name="snapshot">The project after the change.</param>
    /// <param name="json">The document as <c>ProjectJson</c> writes it, serialised once for every client.</param>
    /// <param name="reason">The wire name of the command, or undo or redo.</param>
    /// <param name="cancellationToken">Cancels the announcement.</param>
    public Task ChangedAsync(UserId user, ProjectSnapshot snapshot, string json, string reason, CancellationToken cancellationToken);

    /// <summary>The project was deleted.</summary>
    public Task DeletedAsync(UserId user, ProjectId project, CancellationToken cancellationToken);
}
