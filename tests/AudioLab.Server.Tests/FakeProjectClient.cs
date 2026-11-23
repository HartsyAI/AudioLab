using AudioLab.Core.Realtime;
using AudioLab.Server.Realtime;

namespace AudioLab.Server.Tests;

/// <summary>A client of the hub as the outbox sees it: it records what it is sent, and a test can make it slow.</summary>
public sealed class FakeProjectClient : IProjectClient
{
    private readonly object _sync = new();
    private readonly List<string> _sent = [];
    private TaskCompletionSource? _gate;
    private Exception? _failure;

    public IReadOnlyList<string> Sent
    {
        get
        {
            lock (_sync)
            {
                return [.. _sent];
            }
        }
    }

    public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Makes the next send wait until <see cref="Open"/> is called.</summary>
    public void Close() => _gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

    public void Open() => _gate?.TrySetResult();

    /// <summary>Makes every send fail with <paramref name="exception"/>.</summary>
    public void FailWith(Exception exception) => _failure = exception;

    public async Task DocumentChanged(ProjectChange change)
    {
        Started.TrySetResult();
        if (_failure is { } failure)
        {
            throw failure;
        }
        if (_gate is { } gate)
        {
            await gate.Task;
        }
        lock (_sync)
        {
            _sent.Add($"{change.Project}:{change.Revision}");
        }
    }

    public async Task ProjectDeleted(ProjectClosed closed)
    {
        Started.TrySetResult();
        if (_gate is { } gate)
        {
            await gate.Task;
        }
        lock (_sync)
        {
            _sent.Add($"{closed.Project}:deleted");
        }
    }
}
