using AudioLab.Core.Model;
using AudioLab.Server.Services;
using AudioLab.Server.Store;

namespace AudioLab.Server.Tests;

/// <summary>Records what a project announces, in order, and can hold an announcement so a test can start another call meanwhile.</summary>
public sealed class OrderingEvents : IProjectEvents
{
    private readonly object _sync = new();
    private readonly List<string> _events = [];
    private TaskCompletionSource? _gate;

    public IReadOnlyList<string> Events
    {
        get
        {
            lock (_sync)
            {
                return [.. _events];
            }
        }
    }

    public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public void HoldChanges() => _gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

    public void Release() => _gate?.TrySetResult();

    public async Task ChangedAsync(UserId user, ProjectSnapshot snapshot, string json, string reason, CancellationToken cancellationToken)
    {
        Entered.TrySetResult();
        if (_gate is { } gate)
        {
            await gate.Task;
        }
        lock (_sync)
        {
            _events.Add($"changed:{snapshot.Revision}");
        }
    }

    public Task DeletedAsync(UserId user, ProjectId project, CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            _events.Add("deleted");
        }
        return Task.CompletedTask;
    }
}
