using AudioLab.Core.Model;
using AudioLab.Server.Services;
using AudioLab.Server.Store;

namespace AudioLab.Server.Realtime;

/// <summary>Puts a project's events in the outbox of each connection that joined it. It never waits for a client.</summary>
public sealed class HubProjectEvents(ProjectMembers members) : IProjectEvents
{
    /// <inheritdoc />
    public Task ChangedAsync(UserId user, ProjectSnapshot snapshot, string json, string reason, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ProjectId project = snapshot.Document.Id;
        ProjectChange change = new(project.Value, snapshot.Revision, json, reason);
        foreach (ConnectionState connection in members.Of(user, project))
        {
            connection.Outbox.Post(change);
        }
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task DeletedAsync(UserId user, ProjectId project, CancellationToken cancellationToken)
    {
        ProjectClosed closed = new(project.Value, "deleted");
        foreach (ConnectionState connection in members.Close(user, project))
        {
            connection.Remove(project);
            connection.Outbox.PostDeleted(closed);
        }
        return Task.CompletedTask;
    }
}
