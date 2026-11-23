using AudioLab.Core.Realtime;

namespace AudioLab.Web.Services;

/// <summary>One connection to the project hub, as the UI uses it: join a project, send a command by its wire name, and hear every change.</summary>
public interface IProjectHub : IAsyncDisposable
{
    /// <summary>Raised when the state of the connection changes.</summary>
    public event Action<HubState>? StateChanged;

    /// <summary>Raised for each revision the server sends of a joined project, whoever made it.</summary>
    public event Action<ProjectChange>? DocumentChanged;

    /// <summary>Raised when a joined project was deleted.</summary>
    public event Action<ProjectClosed>? ProjectDeleted;

    /// <summary>The state of the connection now.</summary>
    public HubState State { get; }

    /// <summary>Opens the connection.</summary>
    public Task ConnectAsync(CancellationToken cancellationToken = default);

    /// <summary>Joins a project the user owns and returns its state at that moment.</summary>
    public Task<JoinReply> JoinAsync(string project, CancellationToken cancellationToken = default);

    /// <summary>Leaves a project; no more changes are sent.</summary>
    public Task LeaveAsync(string project, CancellationToken cancellationToken = default);

    /// <summary>Sends a command by its wire name; a refusal is in the reply.</summary>
    public Task<CommandReply> ApplyAsync(
        string project,
        string command,
        string json,
        string? gesture = null,
        CancellationToken cancellationToken = default);

    /// <summary>Goes back one undo step for everyone in the project.</summary>
    public Task<CommandReply> UndoAsync(string project, CancellationToken cancellationToken = default);

    /// <summary>Goes forward one redo step for everyone in the project.</summary>
    public Task<CommandReply> RedoAsync(string project, CancellationToken cancellationToken = default);

    /// <summary>Closes this client's open gesture, as the release of a drag does.</summary>
    public Task EndGestureAsync(string project, string gesture, CancellationToken cancellationToken = default);
}
