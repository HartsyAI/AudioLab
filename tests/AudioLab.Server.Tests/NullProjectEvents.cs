using AudioLab.Core.Model;
using AudioLab.Server.Services;
using AudioLab.Server.Store;

namespace AudioLab.Server.Tests;

/// <summary>Announces nothing, for tests of the store that have no hub.</summary>
public sealed class NullProjectEvents : IProjectEvents
{
    public static NullProjectEvents Instance { get; } = new();

    public Task ChangedAsync(UserId user, ProjectSnapshot snapshot, string json, string reason, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task DeletedAsync(UserId user, ProjectId project, CancellationToken cancellationToken) => Task.CompletedTask;
}
