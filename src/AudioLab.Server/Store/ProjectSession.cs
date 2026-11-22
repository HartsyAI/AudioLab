using AudioLab.Core.Model;

namespace AudioLab.Server.Store;

/// <summary>An open project: its current document, the lock that orders changes to it, and its debounced autosave.</summary>
/// <remarks>
/// A change replaces the whole immutable document under the session's lock and raises the revision. A session leaves
/// use only through <see cref="CloseAsync"/>, which writes what is unsaved first, or <see cref="DiscardAsync"/>, for a
/// deleted project; after either, a change or a save throws <see cref="SessionClosedException"/> instead of being lost.
/// </remarks>
public sealed class ProjectSession
{
    private readonly ProjectStore _store;
    private readonly StoreOptions _options;
    private readonly TimeProvider _time;
    private readonly ILogger _logger;
    private readonly object _state = new();
    private readonly SemaphoreSlim _writes = new(1, 1);
    private readonly ITimer _timer;
    private ProjectSnapshot _snapshot;
    private long _savedRevision;
    private DateTimeOffset _modified;
    private DateTimeOffset? _unsavedSince;
    private DateTimeOffset? _writeStarted;
    private bool _closed;
    private Task _autosave = Task.CompletedTask;

    internal ProjectSession(ProjectStore store, ProjectDocument document, DateTimeOffset modified, StoreOptions options, TimeProvider time, ILogger logger)
    {
        _store = store;
        _options = options;
        _time = time;
        _logger = logger;
        _snapshot = new ProjectSnapshot(document, 1);
        _savedRevision = 1;
        _modified = modified;
        _timer = time.CreateTimer(OnTimer, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    /// <summary>The project's id.</summary>
    public ProjectId Id => Snapshot.Document.Id;

    /// <summary>The user who owns the project.</summary>
    public UserId Owner => Snapshot.Document.Owner;

    /// <summary>The current document and revision.</summary>
    public ProjectSnapshot Snapshot
    {
        get
        {
            lock (_state)
            {
                return _snapshot;
            }
        }
    }

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

    /// <summary>The summary the project's list entry shows.</summary>
    public ProjectSummary Summary
    {
        get
        {
            lock (_state)
            {
                return new ProjectSummary(_snapshot.Document.Id.Value, _snapshot.Document.Name, _modified);
            }
        }
    }

    /// <summary>Replaces the document with the result of <paramref name="change"/>, which runs under the session's lock and must be quick and free of side effects.</summary>
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
            _snapshot = new ProjectSnapshot(next, _snapshot.Revision + 1);
            DateTimeOffset now = _time.GetUtcNow();
            _modified = now;
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
    private void Close()
    {
        _closed = true;
        _timer.Dispose();
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
                    _timer.Change(_options.AutosaveMaxDelay, Timeout.InfiniteTimeSpan);
                }
            }
        }
        finally
        {
            _writes.Release();
        }
    }
}
