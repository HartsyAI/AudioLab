using AudioLab.Core.Realtime;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;

namespace AudioLab.Web.Services;

/// <summary>The project hub over SignalR and WebSockets, with the strict JSON of Core's hub contract.</summary>
public sealed class SignalRProjectHub : IProjectHub
{
    private readonly HubConnection _connection;

    /// <summary>Creates a connection to the hub at <paramref name="hub"/>; it opens on <see cref="ConnectAsync"/>.</summary>
    /// <param name="hub">The hub's address.</param>
    /// <param name="reconnects">Whether the connection opens again by itself after a loss; a one-shot request must not.</param>
    public SignalRProjectHub(Uri hub, bool reconnects)
    {
        ArgumentNullException.ThrowIfNull(hub);
        IHubConnectionBuilder builder = new HubConnectionBuilder()
            .WithUrl(hub, options =>
            {
                options.Transports = HttpTransportType.WebSockets;
                options.SkipNegotiation = true;
            })
            .AddJsonProtocol(json => json.PayloadSerializerOptions.TypeInfoResolver = HubJsonContext.Default);
        _connection = (reconnects ? builder.WithAutomaticReconnect() : builder).Build();
        _connection.On<ProjectChange>(HubProtocol.DocumentChanged, change => DocumentChanged?.Invoke(change));
        _connection.On<ProjectClosed>(HubProtocol.ProjectDeleted, closed => ProjectDeleted?.Invoke(closed));
        _connection.Reconnecting += _ => Raise(HubState.Reconnecting);
        _connection.Reconnected += _ => Raise(HubState.Connected);
        _connection.Closed += _ => Raise(HubState.Disconnected);
    }

    /// <inheritdoc />
    public event Action<HubState>? StateChanged;

    /// <inheritdoc />
    public event Action<ProjectChange>? DocumentChanged;

    /// <inheritdoc />
    public event Action<ProjectClosed>? ProjectDeleted;

    /// <inheritdoc />
    public HubState State { get; private set; }

    /// <inheritdoc />
    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        Set(HubState.Connecting);
        bool started = false;
        try
        {
            await _connection.StartAsync(cancellationToken);
            started = true;
        }
        finally
        {
            Set(started ? HubState.Connected : HubState.Disconnected);
        }
    }

    /// <inheritdoc />
    public Task<JoinReply> JoinAsync(string project, CancellationToken cancellationToken = default)
    {
        return _connection.InvokeAsync<JoinReply>(HubProtocol.Join, project, cancellationToken);
    }

    /// <inheritdoc />
    public Task LeaveAsync(string project, CancellationToken cancellationToken = default)
    {
        return _connection.InvokeAsync(HubProtocol.Leave, project, cancellationToken);
    }

    /// <inheritdoc />
    public Task<CommandReply> ApplyAsync(
        string project,
        string command,
        string json,
        string? gesture = null,
        CancellationToken cancellationToken = default)
    {
        return _connection.InvokeAsync<CommandReply>(HubProtocol.Apply, project, command, json, gesture, cancellationToken);
    }

    /// <inheritdoc />
    public Task<CommandReply> UndoAsync(string project, CancellationToken cancellationToken = default)
    {
        return _connection.InvokeAsync<CommandReply>(HubProtocol.Undo, project, cancellationToken);
    }

    /// <inheritdoc />
    public Task<CommandReply> RedoAsync(string project, CancellationToken cancellationToken = default)
    {
        return _connection.InvokeAsync<CommandReply>(HubProtocol.Redo, project, cancellationToken);
    }

    /// <inheritdoc />
    public Task EndGestureAsync(string project, string gesture, CancellationToken cancellationToken = default)
    {
        return _connection.InvokeAsync(HubProtocol.EndGesture, project, gesture, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        return _connection.DisposeAsync();
    }

    private Task Raise(HubState state)
    {
        Set(state);
        return Task.CompletedTask;
    }

    private void Set(HubState state)
    {
        State = state;
        StateChanged?.Invoke(state);
    }
}
