using System.Globalization;
using AudioLab.Core.Commands;
using AudioLab.Core.Model;
using AudioLab.Core.Realtime;

namespace AudioLab.Web.Services;

/// <summary>An open project that follows the server (D20): it joins over the hub, keeps the newest revision, sends commands.</summary>
/// <remarks>
/// A change is applied only if its revision is above the one held, and one that arrives before the join answers is kept and applied
/// after it. A reconnecting client is a new connection and joins again. Blazor WebAssembly runs on one thread, so nothing here locks.
/// </remarks>
public sealed class LiveProject(IProjectHubFactory hubs, CommandSender sender) : IAsyncDisposable
{
    private static readonly TimeSpan _settle = TimeSpan.FromSeconds(5);

    private readonly List<(long Revision, TaskCompletionSource Done)> _waiters = [];

    private readonly string _stamp = Guid.NewGuid().ToString("N")[..6];

    private IProjectHub? _hub;

    private string? _project;

    private ProjectChange? _early;

    private bool _joined;

    private bool _lost;

    private int _gestures;

    /// <summary>Raised when the document, the connection state or the problem changed.</summary>
    public event Action? Changed;

    /// <summary>The newest document, or null until the first join answers.</summary>
    public ProjectDocument? Document { get; private set; }

    /// <summary>The revision of <see cref="Document"/>.</summary>
    public long Revision { get; private set; }

    /// <summary>Where the connection stands.</summary>
    public HubState State { get; private set; }

    /// <summary>Why the project cannot be followed any more (deleted, or a document this build cannot read), or null.</summary>
    public string? Problem { get; private set; }

    /// <summary>Connects, joins the project and holds its document; every later revision is applied as it is sent.</summary>
    public async Task OpenAsync(string project, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(project);
        _project = project;
        _hub = hubs.Create(reconnects: true);
        _hub.StateChanged += OnState;
        _hub.DocumentChanged += OnChange;
        _hub.ProjectDeleted += OnDeleted;
        await _hub.ConnectAsync(cancellationToken);
        await JoinAsync(cancellationToken);
    }

    /// <summary>Sends a command, and returns once the revision it made has been applied here; a refusal is in the reply.</summary>
    public async Task<CommandReply> SendAsync(Command command, string? gesture = null, CancellationToken cancellationToken = default)
    {
        CommandReply reply = await sender.SendAsync(Hub, command, gesture, cancellationToken);
        await SettleAsync(reply, cancellationToken);
        return reply;
    }

    /// <summary>Goes back one undo step for everyone in the project.</summary>
    public async Task<CommandReply> UndoAsync(CancellationToken cancellationToken = default)
    {
        CommandReply reply = await Hub.UndoAsync(Project, cancellationToken);
        await SettleAsync(reply, cancellationToken);
        return reply;
    }

    /// <summary>Goes forward one redo step for everyone in the project.</summary>
    public async Task<CommandReply> RedoAsync(CancellationToken cancellationToken = default)
    {
        CommandReply reply = await Hub.RedoAsync(Project, cancellationToken);
        await SettleAsync(reply, cancellationToken);
        return reply;
    }

    /// <summary>A gesture id no other client has, for the commands of one drag.</summary>
    public string NewGesture() => string.Create(CultureInfo.InvariantCulture, $"g{_stamp}-{++_gestures}");

    /// <summary>Closes this client's open gesture.</summary>
    public Task EndGestureAsync(string gesture, CancellationToken cancellationToken = default)
    {
        return Hub.EndGestureAsync(Project, gesture, cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_hub is { } hub)
        {
            hub.StateChanged -= OnState;
            hub.DocumentChanged -= OnChange;
            hub.ProjectDeleted -= OnDeleted;
            await hub.DisposeAsync();
        }
        foreach ((long _, TaskCompletionSource done) in _waiters)
        {
            done.TrySetResult();
        }
    }

    private IProjectHub Hub => _hub ?? throw new InvalidOperationException("The project is not open.");

    private string Project => _project ?? throw new InvalidOperationException("The project is not open.");

    private async Task JoinAsync(CancellationToken cancellationToken)
    {
        _joined = false;
        JoinReply reply = await Hub.JoinAsync(Project, cancellationToken);
        Adopt(reply.Revision, reply.Document, force: _lost);
        _joined = true;
        _lost = false;
        if (_early is { } early)
        {
            _early = null;
            Adopt(early.Revision, early.Document, force: false);
        }
        Raise();
    }

    // A change is applied only if it is newer; before the join answers it is kept, the newest of them, for after.
    private void OnChange(ProjectChange change)
    {
        if (!_joined)
        {
            if (_early is null || change.Revision > _early.Revision)
            {
                _early = change;
            }
            return;
        }
        Adopt(change.Revision, change.Document, force: false);
    }

    private void OnDeleted(ProjectClosed closed)
    {
        Problem = "This project was deleted.";
        Raise();
    }

    private void OnState(HubState state)
    {
        State = state;
        if (state is HubState.Reconnecting or HubState.Disconnected)
        {
            _lost = true;
        }
        Raise();
        if (state == HubState.Connected && _lost && _hub is not null)
        {
            _ = RejoinAsync();
        }
    }

    // A reconnect is a new connection with a new client id: it must join again, and the answer replaces what was held.
    private async Task RejoinAsync()
    {
        try
        {
            await JoinAsync(CancellationToken.None);
        }
        catch (Exception exception) when (exception is InvalidOperationException or Microsoft.AspNetCore.SignalR.HubException)
        {
            Problem = "The project could not be joined again after the connection dropped.";
            Raise();
        }
    }

    private void Adopt(long revision, string text, bool force)
    {
        if (!force && revision <= Revision)
        {
            return;
        }
        try
        {
            Document = ProjectJson.Read(text);
        }
        catch (ProjectFormatException)
        {
            Problem = "The server sent a document this version of AudioLab cannot read.";
            Raise();
            return;
        }
        Revision = revision;
        Problem = null;
        foreach ((long Revision, TaskCompletionSource Done) waiter in _waiters.Where(waiter => waiter.Revision <= revision).ToList())
        {
            waiter.Done.TrySetResult();
            _waiters.Remove(waiter);
        }
        if (_joined)
        {
            Raise();
        }
    }

    // After a reply the document of that revision, or a later one, is on its way to everyone, this client included.
    private async Task SettleAsync(CommandReply reply, CancellationToken cancellationToken)
    {
        if (!reply.Succeeded || !reply.Changed || Revision >= reply.Revision)
        {
            return;
        }
        TaskCompletionSource done = new(TaskCreationOptions.RunContinuationsAsynchronously);
        _waiters.Add((reply.Revision, done));
        await Task.WhenAny(done.Task, Task.Delay(_settle, cancellationToken));
    }

    private void Raise() => Changed?.Invoke();
}
