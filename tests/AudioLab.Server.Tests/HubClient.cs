using System.Threading.Channels;
using AudioLab.Core.Realtime;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Http.Connections.Client;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

namespace AudioLab.Server.Tests;

/// <summary>A real SignalR client of the running server, over WebSockets, that records what the server sends it.</summary>
public sealed class HubClient : IAsyncDisposable
{
    public static readonly TimeSpan Wait = TimeSpan.FromSeconds(10);

    private readonly Channel<ProjectChange> _changes = Channel.CreateUnbounded<ProjectChange>();
    private readonly Channel<ProjectClosed> _closed = Channel.CreateUnbounded<ProjectClosed>();

    private HubClient(HubConnection connection)
    {
        Connection = connection;
        connection.On<ProjectChange>("DocumentChanged", change => _changes.Writer.TryWrite(change));
        connection.On<ProjectClosed>("ProjectDeleted", closed => _closed.Writer.TryWrite(closed));
    }

    public HubConnection Connection { get; }

    public string ConnectionId => Connection.ConnectionId!;

    public static async Task<HubClient> ConnectAsync(RunningServer server, Action<HttpConnectionOptions>? configure = null)
    {
        HubClient client = Build(server, configure);
        await client.Connection.StartAsync();
        return client;
    }

    public static HubClient Build(RunningServer server, Action<HttpConnectionOptions>? configure = null)
    {
        HubConnection connection = new HubConnectionBuilder()
            .WithUrl(new Uri(server.Client.BaseAddress!, server.HubPath), options =>
            {
                options.Transports = HttpTransportType.WebSockets;
                configure?.Invoke(options);
            })
            .AddJsonProtocol(json => json.PayloadSerializerOptions.TypeInfoResolver = HubJsonContext.Default)
            .Build();
        return new HubClient(connection);
    }

    public Task<JoinReply> JoinAsync(string project) => Connection.InvokeAsync<JoinReply>("Join", project);

    public Task LeaveAsync(string project) => Connection.InvokeAsync("Leave", project);

    public Task<CommandReply> ApplyAsync(string project, string command, string json, string? gesture = null)
    {
        return Connection.InvokeAsync<CommandReply>("Apply", project, command, json, gesture);
    }

    public Task<CommandReply> UndoAsync(string project) => Connection.InvokeAsync<CommandReply>("Undo", project);

    public Task<CommandReply> RedoAsync(string project) => Connection.InvokeAsync<CommandReply>("Redo", project);

    public Task EndGestureAsync(string project, string gesture) => Connection.InvokeAsync("EndGesture", project, gesture);

    public async Task<ProjectChange> NextChangeAsync()
    {
        using CancellationTokenSource timeout = new(Wait);
        return await _changes.Reader.ReadAsync(timeout.Token);
    }

    public async Task<ProjectClosed> NextClosedAsync()
    {
        using CancellationTokenSource timeout = new(Wait);
        return await _closed.Reader.ReadAsync(timeout.Token);
    }

    public async Task<bool> HeardNothingAsync()
    {
        await Task.Delay(300);
        return !_changes.Reader.TryPeek(out _) && !_closed.Reader.TryPeek(out _);
    }

    public async ValueTask DisposeAsync() => await Connection.DisposeAsync();
}
