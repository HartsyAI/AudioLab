using AudioLab.Core.Model;
using AudioLab.Server.Services;
using AudioLab.Server.Store;
using Microsoft.AspNetCore.SignalR;

namespace AudioLab.Server.Realtime;

/// <summary>Sends a project's events to the hub group of its clients.</summary>
public sealed class HubProjectEvents(IHubContext<ProjectHub, IProjectClient> hub) : IProjectEvents
{
    /// <inheritdoc />
    public Task ChangedAsync(UserId user, ProjectSnapshot snapshot, string reason, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ProjectDocument document = snapshot.Document;
        ProjectChange change = new(document.Id.Value, snapshot.Revision, ProjectJson.Write(document), reason);
        return hub.Clients.Group(ProjectGroups.Of(user, document.Id)).DocumentChanged(change);
    }

    /// <inheritdoc />
    public Task DeletedAsync(UserId user, ProjectId project, CancellationToken cancellationToken)
    {
        return hub.Clients.Group(ProjectGroups.Of(user, project)).ProjectDeleted(new ProjectClosed(project.Value, "deleted"));
    }
}
