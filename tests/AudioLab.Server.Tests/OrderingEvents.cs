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
    private TaskCompletionSource? _deleteGate;

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

    public TaskCompletionSource DeleteEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public void Release() => _gate?.TrySetResult();

    public void HoldDeletion() => _deleteGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

    public void ReleaseDeletion() => _deleteGate?.TrySetResult();

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

    public async Task DeletedAsync(UserId user, ProjectId project, CancellationToken cancellationToken)
    {
        DeleteEntered.TrySetResult();
        if (_deleteGate is { } gate)
        {
            await gate.Task;
        }
        lock (_sync)
        {
            _events.Add("deleted");
        }
    }
}
