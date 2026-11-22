using AudioLab.Core.Commands;
using AudioLab.Core.Realtime;
using AudioLab.Web.Services;

namespace AudioLab.Web.Tests;

/// <summary>A hub connection that records what is sent and answers with a prepared reply.</summary>
internal sealed class FakeProjectHub : IProjectHub
{
    public List<string> Calls { get; } = [];

    public CommandReply Reply { get; set; } = new(true, true, 2, [], []);

    public bool Disposed { get; private set; }

    public event Action<HubState>? StateChanged;

    public event Action<ProjectChange>? DocumentChanged;

    public event Action<ProjectClosed>? ProjectDeleted;

    public HubState State { get; private set; }

    public void Push(ProjectChange change) => DocumentChanged?.Invoke(change);

    public void Close(ProjectClosed closed) => ProjectDeleted?.Invoke(closed);

    public void Move(HubState state)
    {
        State = state;
        StateChanged?.Invoke(state);
    }

    public Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        Calls.Add("connect");
        Move(HubState.Connected);
        return Task.CompletedTask;
    }

    public Task<JoinReply> JoinAsync(string project, CancellationToken cancellationToken = default)
    {
        Calls.Add($"join {project}");
        return Task.FromResult(new JoinReply(project, 1, "{}"));
    }

    public Task LeaveAsync(string project, CancellationToken cancellationToken = default)
    {
        Calls.Add($"leave {project}");
        return Task.CompletedTask;
    }

    public Task<CommandReply> ApplyAsync(
        string project,
        string command,
        string json,
        string? gesture = null,
        CancellationToken cancellationToken = default)
    {
        Calls.Add($"apply {project} {command} {json} {gesture}".TrimEnd());
        return Task.FromResult(Reply);
    }

    public Task<CommandReply> UndoAsync(string project, CancellationToken cancellationToken = default)
    {
        Calls.Add($"undo {project}");
        return Task.FromResult(Reply);
    }

    public Task<CommandReply> RedoAsync(string project, CancellationToken cancellationToken = default)
    {
        Calls.Add($"redo {project}");
        return Task.FromResult(Reply);
    }

    public Task EndGestureAsync(string project, string gesture, CancellationToken cancellationToken = default)
    {
        Calls.Add($"end {project} {gesture}");
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        Disposed = true;
        Calls.Add("dispose");
        return ValueTask.CompletedTask;
    }

    public static CommandReply Refused(CommandErrorCode code, string message) => new(false, false, 1, [new HubCommandError(code, message)], []);
}
