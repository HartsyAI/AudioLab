using AudioLab.Core.Model;
using AudioLab.Server.Store;

namespace AudioLab.Server.Services;

/// <summary>Announces nothing, for a server with no hub and for tests of the store.</summary>
public sealed class NullProjectEvents : IProjectEvents
{
    /// <summary>The one instance.</summary>
    public static NullProjectEvents Instance { get; } = new();

    /// <inheritdoc />
    public Task ChangedAsync(UserId user, ProjectSnapshot snapshot, string reason, CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public Task DeletedAsync(UserId user, ProjectId project, CancellationToken cancellationToken) => Task.CompletedTask;
}
