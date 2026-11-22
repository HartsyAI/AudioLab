using System.Threading.Channels;
using Microsoft.AspNetCore.SignalR;

namespace AudioLab.Server.Realtime;

/// <summary>What one connection is yet to be sent: the newest change of each project and any deletion, sent by a writer loop so announcing never waits for a client; a send that takes too long aborts the connection.</summary>
/// <remarks>A newer change replaces an older unsent one, which is safe because a change holds the whole document. A deletion is never replaced.</remarks>
public sealed class ConnectionOutbox : IDisposable
{
    private readonly object _sync = new();
    private readonly Dictionary<string, Slot> _slots = [];
    private readonly Queue<string> _order = [];
    private readonly Channel<bool> _signal = Channel.CreateBounded<bool>(new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropWrite });
    private readonly CancellationTokenSource _stop = new();
    private readonly IHubContext<ProjectHub, IProjectClient> _hub;
    private readonly string _connectionId;
    private readonly Action _abort;
    private readonly TimeSpan _sendTimeout;
    private readonly ILogger _logger;

    /// <summary>Starts the writer loop for one connection.</summary>
    /// <param name="hub">Sends to the connection.</param>
    /// <param name="connectionId">The connection to send to.</param>
    /// <param name="abort">Closes the connection when a send does not finish in <paramref name="sendTimeout"/>.</param>
    /// <param name="sendTimeout">The longest one message may take to be handed to the client's connection.</param>
    /// <param name="logger">Logs an aborted connection.</param>
    public ConnectionOutbox(IHubContext<ProjectHub, IProjectClient> hub, string connectionId, Action abort, TimeSpan sendTimeout, ILogger logger)
    {
        _hub = hub;
        _connectionId = connectionId;
        _abort = abort;
        _sendTimeout = sendTimeout;
        _logger = logger;
        _ = Task.Run(RunAsync);
    }

    /// <summary>Queues a change; it replaces an older change of the same project that was not sent yet. Never waits.</summary>
    public void Post(ProjectChange change)
    {
        ArgumentNullException.ThrowIfNull(change);
        lock (_sync)
        {
            Slot slot = SlotOf(change.Project);
            if (slot.Deleted is null && (slot.Change is null || change.Revision > slot.Change.Revision))
            {
                slot.Change = change;
            }
        }
        _signal.Writer.TryWrite(true);
    }

    /// <summary>Queues the end of a project; it drops a change not sent yet, since the project is gone, and is never replaced. Never waits.</summary>
    public void PostDeleted(ProjectClosed closed)
    {
        ArgumentNullException.ThrowIfNull(closed);
        lock (_sync)
        {
            Slot slot = SlotOf(closed.Project);
            slot.Change = null;
            slot.Deleted = closed;
        }
        _signal.Writer.TryWrite(true);
    }

    /// <summary>Stops the writer loop; what was not sent is dropped.</summary>
    public void Dispose()
    {
        _stop.Cancel();
        _signal.Writer.TryComplete();
    }

    // Called under _sync.
    private Slot SlotOf(string project)
    {
        if (!_slots.TryGetValue(project, out Slot? slot))
        {
            slot = new Slot();
            _slots[project] = slot;
            _order.Enqueue(project);
        }
        return slot;
    }

    private Slot? Take()
    {
        lock (_sync)
        {
            if (!_order.TryDequeue(out string? project))
            {
                return null;
            }
            Slot slot = _slots[project];
            _slots.Remove(project);
            return slot;
        }
    }

    private async Task RunAsync()
    {
        try
        {
            while (await _signal.Reader.WaitToReadAsync(_stop.Token))
            {
                _signal.Reader.TryRead(out _);
                while (Take() is { } slot)
                {
                    if (!await SendAsync(slot))
                    {
                        return;
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // The connection ended and the outbox was disposed: nothing is left to send.
        }
    }

    private async Task<bool> SendAsync(Slot slot)
    {
        Task? send = null;
        try
        {
            IProjectClient client = _hub.Clients.Client(_connectionId);
            send = slot.Deleted is { } closed ? client.ProjectDeleted(closed) : client.DocumentChanged(slot.Change!);
            await send.WaitAsync(_sendTimeout, _stop.Token);
            return true;
        }
        catch (TimeoutException)
        {
            HubLog.SendTimedOut(_logger, (long)_sendTimeout.TotalMilliseconds);
            Abandon(send);
            return false;
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested)
        {
            // The outbox was disposed because the connection ended; there is nothing to abort.
            ObserveLater(send);
            return false;
        }
        catch (Exception exception)
        {
            // The loop ends here, so the client would silently stop hearing changes while it stayed connected: it is disconnected
            // instead, and joins again.
            HubLog.SendFailed(_logger, exception);
            Abandon(send);
            return false;
        }
    }

    private void Abandon(Task? send)
    {
        try
        {
            _abort();
        }
        catch (Exception exception)
        {
            // Aborting a connection that is already gone can fail; the loop ends either way and must not end faulted.
            HubLog.SendFailed(_logger, exception);
        }
        ObserveLater(send);
    }

    // A send that is abandoned may still fail later; its exception is then taken so it is never unobserved.
    private static void ObserveLater(Task? send)
    {
        _ = send?.ContinueWith(task => task.Exception, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously);
    }

    private sealed class Slot
    {
        public ProjectChange? Change { get; set; }

        public ProjectClosed? Deleted { get; set; }
    }
}
