using System.Text;
using AudioLab.Core.Commands;
using AudioLab.Core.Model;

namespace AudioLab.Server.Store;

/// <summary>An open project: its current document, the lock that orders changes to it, and its debounced autosave.</summary>
/// <remarks>
/// A change replaces the whole immutable document under the session's lock and raises the revision. A session leaves
/// use only through <see cref="CloseAsync"/>, which writes what is unsaved first, or <see cref="DiscardAsync"/>, for a
/// deleted project; after either, a change or a save throws <see cref="SessionClosedException"/> instead of being lost.
/// Commands go through <see cref="Apply"/>, <see cref="Undo"/> and <see cref="Redo"/>, which keep the project's undo history.
/// </remarks>
public sealed class ProjectSession
{
    /// <summary>The longest wait between retries of a failing autosave.</summary>
    public static readonly TimeSpan MaxRetryDelay = TimeSpan.FromMinutes(5);

    private readonly ProjectStore _store;
    private readonly StoreOptions _options;
    private readonly TimeProvider _time;
    private readonly ILogger _logger;
    private readonly CommandRegistry _registry;
    private readonly ReplayableIds _ids;
    private readonly object _state = new();
    private readonly SemaphoreSlim _writes = new(1, 1);
    private readonly ITimer _timer;
    private ProjectSnapshot _snapshot;
    private long _savedRevision;
    private long _modifiedTicks;
    private DateTimeOffset? _unsavedSince;
    private DateTimeOffset? _writeStarted;
    private CommandHistory? _history;
    private int _failures;
    private bool _closed;
    private Task _autosave = Task.CompletedTask;

    internal ProjectSession(
        ProjectStore store,
        ProjectDocument document,
        DateTimeOffset modified,
        StoreOptions options,
        TimeProvider time,
        CommandRegistry registry,
        IIdService ids,
        ILogger logger)
    {
        _store = store;
        _registry = registry;
        _ids = new ReplayableIds(ids);
        _options = options;
        _time = time;
        _logger = logger;
        _snapshot = new ProjectSnapshot(document, 1);
        _savedRevision = 1;
        _modifiedTicks = modified.UtcTicks;
        _timer = time.CreateTimer(OnTimer, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    /// <summary>Whether the session has been closed or discarded, so a change to it would be refused.</summary>
    public bool IsClosed
    {
        get
        {
            lock (_state)
            {
                return _closed;
            }
        }
    }

    /// <summary>The project's id.</summary>
    public ProjectId Id => Snapshot.Document.Id;

    /// <summary>The user who owns the project.</summary>
    public UserId Owner => Snapshot.Document.Owner;

    /// <summary>The current document and revision. Read without the session's lock, so a command in progress never delays it: it is the last one committed.</summary>
    public ProjectSnapshot Snapshot => Volatile.Read(ref _snapshot);

    /// <summary>The newest revision known to be on disk.</summary>
    public long SavedRevision
    {
        get
        {
            lock (_state)
            {
                return _savedRevision;
            }
        }
    }

    /// <summary>Whether the document has changes that are not on disk.</summary>
    public bool IsUnsaved
    {
        get
        {
            lock (_state)
            {
                return _snapshot.Revision != _savedRevision;
            }
        }
    }

    /// <summary>The summary the project's list entry shows, read without the session's lock.</summary>
    public ProjectSummary Summary
    {
        get
        {
            ProjectDocument document = Snapshot.Document;
            return new ProjectSummary(document.Id.Value, document.Name, new DateTimeOffset(Volatile.Read(ref _modifiedTicks), TimeSpan.Zero));
        }
    }

    /// <summary>Replaces the document with the result of <paramref name="change"/>, which runs under the session's lock and must be quick and free of side effects.</summary>
    /// <remarks>A direct change is not a command, so it drops the undo history: the next command starts a new one.</remarks>
    /// <returns>The new snapshot, or the current one when the change leaves the document equal to what it was.</returns>
    /// <exception cref="SessionClosedException">The session is closed.</exception>
    /// <exception cref="ProjectFormatException">The result breaks a rule of <see cref="ProjectValidator"/>; the document is left as it was.</exception>
    public ProjectSnapshot Update(Func<ProjectDocument, ProjectDocument> change)
    {
        ArgumentNullException.ThrowIfNull(change);
        lock (_state)
        {
            if (_closed)
            {
                throw new SessionClosedException();
            }
            ProjectDocument current = _snapshot.Document;
            ProjectDocument next = change(current) ?? throw new InvalidOperationException("A change must return a document.");
            if (next == current)
            {
                return _snapshot;
            }
            if (next.Id != current.Id || next.Owner != current.Owner)
            {
                throw new InvalidOperationException("A change cannot give a project another id or owner.");
            }
            IReadOnlyList<string> problems = ProjectValidator.Validate(next);
            if (problems.Count > 0)
            {
                throw new ProjectFormatException("The change breaks the version 4 rules:", problems);
            }
            _history = null;
            return Commit(next);
        }
    }

    /// <summary>Applies a command for a client, keeping the undo history; a refusal changes nothing.</summary>
    /// <param name="command">The command, already parsed by the registry.</param>
    /// <param name="client">The connection or agent that sent it, as the server named it.</param>
    /// <param name="gesture">The client's gesture the command belongs to, or null for a single edit.</param>
    /// <exception cref="SessionClosedException">The session is closed.</exception>
    public EditOutcome Apply(Command command, ClientId client, GestureId? gesture)
    {
        ArgumentNullException.ThrowIfNull(command);
        lock (_state)
        {
            RequireOpenLocked();
            CommandHistory history = History();

            // Run the command once to see how large the project would become; a document the store would refuse to write
            // is never applied or announced. The second run gets the same ids, so it makes the same document.
            _ids.Record();
            CommandResult preview;
            IReadOnlyList<string> recorded;
            try
            {
                preview = _registry.Execute(history.Current, command, _ids);
            }
            finally
            {
                recorded = _ids.StopRecording();
            }
            string? json = null;
            ProjectDocument? previewed = null;
            if (preview.Document is { } document && document != history.Current)
            {
                json = ProjectJson.Write(document);
                previewed = document;
                if (Encoding.UTF8.GetByteCount(json) > _options.MaxFileBytes)
                {
                    CommandResult tooLarge = CommandResult.Refuse(CommandErrorCode.Limit, $"The project would be larger than the {_options.MaxFileBytes} bytes the server stores.");
                    return new EditOutcome(tooLarge, _snapshot, false, null);
                }
            }
            _ids.Replay(recorded);
            CommandResult result;
            try
            {
                result = history.Apply(command, client, gesture);
            }
            finally
            {
                _ids.EndReplay();
            }
            return Outcome(result, history, history.Current == previewed ? json : null);
        }
    }

    /// <summary>Goes back one undo step; null when there is nothing to undo.</summary>
    /// <exception cref="SessionClosedException">The session is closed.</exception>
    public EditOutcome? Undo()
    {
        lock (_state)
        {
            RequireOpenLocked();
            CommandHistory history = History();
            return history.Undo() is null ? null : Outcome(null, history);
        }
    }

    /// <summary>Goes forward one redo step; null when there is nothing to redo.</summary>
    /// <exception cref="SessionClosedException">The session is closed.</exception>
    public EditOutcome? Redo()
    {
        lock (_state)
        {
            RequireOpenLocked();
            CommandHistory history = History();
            return history.Redo() is null ? null : Outcome(null, history);
        }
    }

    /// <summary>What undo and redo hold now; nothing for a project that no command has changed since it was opened.</summary>
    public HistoryState HistoryState()
    {
        lock (_state)
        {
            return _history is { } history
                ? new HistoryState(_snapshot.Revision, history.UndoCount, history.RedoCount, history.UndoLabel, history.RedoLabel)
                : new HistoryState(_snapshot.Revision, 0, 0, null, null);
        }
    }

    /// <summary>Closes the client's open gesture if it is <paramref name="gesture"/>; the document does not change.</summary>
    public void EndGesture(ClientId client, GestureId gesture)
    {
        lock (_state)
        {
            _history?.EndGesture(client, gesture);
        }
    }

    /// <summary>The gesture the client has open, or null.</summary>
    public GestureId? OpenGestureOf(ClientId client)
    {
        lock (_state)
        {
            return _history?.OpenGestureOf(client);
        }
    }

    /// <summary>Closes whatever gesture the client has open, as when its connection ends; the document does not change.</summary>
    public void ReleaseClient(ClientId client)
    {
        lock (_state)
        {
            _history?.ReleaseClient(client);
        }
    }

    /// <summary>Writes the current document if it has unsaved changes, and returns the revision that is on disk.</summary>
    /// <exception cref="SessionClosedException">The session is closed.</exception>
    public async Task<long> SaveAsync(CancellationToken cancellationToken)
    {
        await _writes.WaitAsync(cancellationToken);
        try
        {
            RequireOpen();
            return await WriteCurrentAsync(cancellationToken);
        }
        finally
        {
            _writes.Release();
        }
    }

    /// <summary>Completes when no autosave is running; changes made after the call may start another.</summary>
    public Task SettleAsync()
    {
        lock (_state)
        {
            return _autosave;
        }
    }

    /// <summary>Writes everything unsaved, then closes the session. If the write fails the session stays open and unsaved.</summary>
    public async Task CloseAsync(CancellationToken cancellationToken)
    {
        await _writes.WaitAsync(cancellationToken);
        try
        {
            while (true)
            {
                await WriteCurrentAsync(cancellationToken);
                lock (_state)
                {
                    if (_snapshot.Revision == _savedRevision)
                    {
                        Close();
                        return;
                    }
                }
            }
        }
        finally
        {
            _writes.Release();
        }
    }

    /// <summary>Closes the session without writing, once any write in flight has finished; for a project that has been deleted.</summary>
    public async Task DiscardAsync(CancellationToken cancellationToken)
    {
        await _writes.WaitAsync(cancellationToken);
        try
        {
            lock (_state)
            {
                Close();
            }
        }
        finally
        {
            _writes.Release();
        }
    }

    private static TimeSpan Min(TimeSpan a, TimeSpan b) => a <= b ? a : b;

    // Called under _state.
    private CommandHistory History() => _history ??= new CommandHistory(_snapshot.Document, _registry, _ids);

    // Called under _state. The history already holds the new document, so a change that leaves it equal adds no revision.
    private EditOutcome Outcome(CommandResult? result, CommandHistory history, string? json = null)
    {
        if (result is { Succeeded: false } || history.Current == _snapshot.Document)
        {
            return new EditOutcome(result, _snapshot, false, null);
        }
        return new EditOutcome(result, Commit(history.Current), true, json ?? ProjectJson.Write(history.Current));
    }

    // Called under _state with a document that has been validated: raises the revision and schedules the autosave.
    private ProjectSnapshot Commit(ProjectDocument next)
    {
        Volatile.Write(ref _snapshot, new ProjectSnapshot(next, _snapshot.Revision + 1));
        DateTimeOffset now = _time.GetUtcNow();
        Volatile.Write(ref _modifiedTicks, now.UtcTicks);
        if (_writeStarted is { } started)
        {
            // The write in flight covers the changes before it started; these are new and get their own full quiet period.
            if (_unsavedSince is not { } since || since <= started)
            {
                _unsavedSince = now;
            }
        }
        else
        {
            _unsavedSince ??= now;
        }
        TimeSpan wait = Min(_options.AutosaveDelay, _unsavedSince.Value + _options.AutosaveMaxDelay - now);
        _timer.Change(wait < TimeSpan.Zero ? TimeSpan.Zero : wait, Timeout.InfiniteTimeSpan);
        return _snapshot;
    }

    // Called under _state.
    private void RequireOpenLocked()
    {
        if (_closed)
        {
            throw new SessionClosedException();
        }
    }

    // Called under _state.
    private void Close()
    {
        _closed = true;
        _timer.Dispose();
    }

    // Doubles from the autosave maximum on each failure in a row and stops at five minutes, so a disk that stays broken is tried,
    // and logged, a few times an hour and not every fifteen seconds.
    private TimeSpan RetryDelay(int failures)
    {
        TimeSpan delay = _options.AutosaveMaxDelay;
        for (int i = 1; i < failures && delay < MaxRetryDelay; i++)
        {
            delay *= 2;
        }
        return Min(delay, MaxRetryDelay);
    }

    private void RequireOpen()
    {
        lock (_state)
        {
            if (_closed)
            {
                throw new SessionClosedException();
            }
        }
    }

    // Called with _writes held, so two writes never run at once and an older revision never lands after a newer one.
    private async Task<long> WriteCurrentAsync(CancellationToken cancellationToken)
    {
        ProjectSnapshot snapshot;
        lock (_state)
        {
            snapshot = _snapshot;
            if (snapshot.Revision == _savedRevision)
            {
                return snapshot.Revision;
            }
            _writeStarted = _time.GetUtcNow();
        }
        bool written = false;
        try
        {
            await _store.WriteAsync(snapshot.Document, cancellationToken);
            written = true;
        }
        finally
        {
            lock (_state)
            {
                _writeStarted = null;
                if (written)
                {
                    _failures = 0;
                    _savedRevision = snapshot.Revision;
                    _unsavedSince = _snapshot.Revision == snapshot.Revision ? null : _unsavedSince;
                }
                else
                {
                    // A write that failed or was refused must not leave an expired wait, or the next change would try again at once.
                    _unsavedSince = null;
                }
            }
        }
        return snapshot.Revision;
    }

    private void OnTimer(object? state)
    {
        Task autosave = AutosaveAsync();
        lock (_state)
        {
            _autosave = autosave;
        }
    }

    private async Task AutosaveAsync()
    {
        await _writes.WaitAsync();
        try
        {
            lock (_state)
            {
                if (_closed)
                {
                    return;
                }
            }
            await WriteCurrentAsync(CancellationToken.None);
        }
        catch (ProjectFormatException exception)
        {
            // A document the store will not write, such as one over the size limit, fails the same way again; the next change retries.
            StoreLog.AutosaveRefused(_logger, Id.Value, exception);
        }
        catch (Exception exception)
        {
            // This runs on a timer with nobody awaiting it: an unobserved failure would be lost, so it is logged and the write is retried.
            StoreLog.AutosaveFailed(_logger, Id.Value, exception);
            lock (_state)
            {
                if (!_closed)
                {
                    _failures++;
                    _timer.Change(RetryDelay(_failures), Timeout.InfiniteTimeSpan);
                }
            }
        }
        finally
        {
            _writes.Release();
        }
    }
}
