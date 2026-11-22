using AudioLab.Core.Commands;
using AudioLab.Core.Model;
using AudioLab.Core.Realtime;
using AudioLab.Web.Services;

namespace AudioLab.Web.Tests;

/// <summary>A hub connection that records what is sent and answers with a prepared reply.</summary>
internal sealed class FakeProjectHub : IProjectHub
{
    /// <summary>Calls.</summary>
    public List<string> Calls { get; } = [];

    /// <summary>Reply.</summary>
    public CommandReply Reply { get; set; } = new(true, true, 2, [], []);

    /// <summary>Disposed.</summary>
    public bool Disposed { get; private set; }

    public List<(string Command, string Json, string? Gesture)> Sent { get; } = [];

    /// <summary>Throw on apply.</summary>
    public Exception? ThrowOnApply { get; set; }

    /// <summary>Join document.</summary>
    public string JoinDocument { get; set; } = ProjectJson.Write(Song.Empty());

    /// <summary>Join revision.</summary>
    public long JoinRevision { get; set; } = 1;

    /// <summary>Before join returns.</summary>
    public Func<Task>? BeforeJoinReturns { get; set; }

    /// <summary>State changed.</summary>
    public event Action<HubState>? StateChanged;

    /// <summary>Document changed.</summary>
    public event Action<ProjectChange>? DocumentChanged;

    /// <summary>Project deleted.</summary>
    public event Action<ProjectClosed>? ProjectDeleted;

    /// <summary>State.</summary>
    public HubState State { get; private set; }

    /// <summary>Push.</summary>
    public void Push(ProjectChange change) => DocumentChanged?.Invoke(change);

    /// <summary>Close.</summary>
    public void Close(ProjectClosed closed) => ProjectDeleted?.Invoke(closed);

    /// <summary>Move.</summary>
    public void Move(HubState state)
    {
        State = state;
        StateChanged?.Invoke(state);
    }

    /// <summary>Connect async.</summary>
    public Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        Calls.Add("connect");
        Move(HubState.Connected);
        return Task.CompletedTask;
    }

    /// <summary>Join async.</summary>
    public async Task<JoinReply> JoinAsync(string project, CancellationToken cancellationToken = default)
    {
        Calls.Add($"join {project}");
        if (BeforeJoinReturns is { } before)
        {
            await before();
        }
        return new JoinReply(project, JoinRevision, JoinDocument);
    }

    /// <summary>Leave async.</summary>
    public Task LeaveAsync(string project, CancellationToken cancellationToken = default)
    {
        Calls.Add($"leave {project}");
        return Task.CompletedTask;
    }

    /// <summary>Apply async.</summary>
    public Task<CommandReply> ApplyAsync(
        string project,
        string command,
        string json,
        string? gesture = null,
        CancellationToken cancellationToken = default)
    {
        if (ThrowOnApply is { } failure)
        {
            throw failure;
        }
        Sent.Add((command, json, gesture));
        Calls.Add($"apply {project} {command} {json} {gesture}".TrimEnd());
        return Task.FromResult(Reply);
    }

    /// <summary>Undo async.</summary>
    public Task<CommandReply> UndoAsync(string project, CancellationToken cancellationToken = default)
    {
        Calls.Add($"undo {project}");
        return Task.FromResult(Reply);
    }

    /// <summary>Redo async.</summary>
    public Task<CommandReply> RedoAsync(string project, CancellationToken cancellationToken = default)
    {
        Calls.Add($"redo {project}");
        return Task.FromResult(Reply);
    }

    /// <summary>End gesture async.</summary>
    public Task EndGestureAsync(string project, string gesture, CancellationToken cancellationToken = default)
    {
        Calls.Add($"end {project} {gesture}");
        return Task.CompletedTask;
    }

    /// <summary>Dispose async.</summary>
    public ValueTask DisposeAsync()
    {
        Disposed = true;
        Calls.Add("dispose");
        return ValueTask.CompletedTask;
    }

    /// <summary>Refused.</summary>
    public static CommandReply Refused(CommandErrorCode code, string message) => new(false, false, 1, [new HubCommandError(code, message)], []);
}
