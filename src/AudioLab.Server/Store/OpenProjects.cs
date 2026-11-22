using AudioLab.Core.Commands;
using AudioLab.Core.Model;
using AudioLab.Core.Rules;
using AudioLab.Server.Media;

namespace AudioLab.Server.Store;

/// <summary>The open projects: one <see cref="ProjectSession"/> per project, so every client and agent edits the same document.</summary>
/// <remarks>
/// Each project has its own lock, so loading, closing or deleting one never waits on another; a short lock guards only
/// the table of locks. A session stays until it is closed, deleted or the server stops, because the hub will tie its
/// lifetime to its clients. A session never leaves the registry before its unsaved changes are on disk.
/// </remarks>
public sealed class OpenProjects(
    ProjectStore store,
    StoreOptions options,
    TimeProvider time,
    CommandRegistry registry,
    MediaStore media,
    IIdService ids,
    ILogger<OpenProjects> logger)
    : IHostedService, IAsyncDisposable
{
    private readonly object _table = new();
    private readonly Dictionary<SessionKey, Entry> _entries = [];

    /// <summary>Returns the project's session, loading and checking the project from disk if it is not open yet.</summary>
    /// <exception cref="ProjectNotFoundException">The user has no such project.</exception>
    /// <exception cref="ProjectFormatException">The stored file is not a valid project of this user.</exception>
    public async Task<ProjectSession> OpenAsync(UserId user, ProjectId id, CancellationToken cancellationToken)
    {
        SessionKey key = new(user, id);
        Entry entry = Acquire(key);
        try
        {
            await entry.Lock.WaitAsync(cancellationToken);
            try
            {
                if (SessionOf(entry) is not { } session)
                {
                    ProjectDocument document = await store.ReadAsync(user, id, cancellationToken);
                    await RequireMediaAsync(document, cancellationToken);
                    session = new ProjectSession(store, document, store.ModifiedAt(user, id), options, time, registry, media, ids, logger);
                    SetSession(entry, session);
                }
                return session;
            }
            finally
            {
                entry.Lock.Release();
            }
        }
        finally
        {
            Release(key, entry);
        }
    }

    /// <summary>Refuses a document that names media its owner does not have, or plays them at another length than the file has.</summary>
    /// <remarks>A document read from a file has not been through a command. A missing file and another user's are told alike.</remarks>
    /// <exception cref="ProjectFormatException">The document names such media; the problems are listed.</exception>
    public async Task RequireMediaAsync(ProjectDocument document, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        await media.PrepareAsync(document.Owner).WaitAsync(cancellationToken);
        IReadOnlyList<string> problems = MediaRules.DocumentProblems(document, media);
        if (problems.Count > 0)
        {
            throw new ProjectFormatException("The project names media the user does not have:", problems);
        }
    }

    /// <summary>Takes the project's publish order, which a change is applied and announced under, and so are a join and a deletion.</summary>
    /// <remarks>The order belongs to the project, not to its session, so a deletion can take it for a project that is not open and a join cannot slip in beside it. Dispose the result to give the order up.</remarks>
    public async Task<IDisposable> EnterOrderAsync(UserId user, ProjectId id, CancellationToken cancellationToken)
    {
        SessionKey key = new(user, id);
        Entry entry = Acquire(key);
        try
        {
            await entry.Order.WaitAsync(cancellationToken);
        }
        catch
        {
            Release(key, entry);
            throw;
        }
        return new OrderLease(this, key, entry);
    }

    /// <summary>The session of an open project, or null when it is not open; it never opens the project.</summary>
    public ProjectSession? SessionIfOpen(UserId user, ProjectId id)
    {
        lock (_table)
        {
            return _entries.TryGetValue(new SessionKey(user, id), out Entry? entry) ? entry.Session : null;
        }
    }

    /// <summary>The current snapshot of an open project, or null when it is not open; it never opens the project.</summary>
    public ProjectSnapshot? SnapshotIfOpen(UserId user, ProjectId id)
    {
        // The snapshot is read outside the table's lock: it takes the session's lock, which a command in progress holds.
        return SessionIfOpen(user, id)?.Snapshot;
    }

    /// <summary>The sessions a user has open.</summary>
    public IReadOnlyList<ProjectSession> SessionsOf(UserId user)
    {
        lock (_table)
        {
            return [.. _entries.Where(pair => pair.Key.User == user).Select(pair => pair.Value.Session).OfType<ProjectSession>()];
        }
    }

    /// <summary>Writes a project's unsaved changes and closes its session; if the write fails the session stays open.</summary>
    public async Task CloseAsync(UserId user, ProjectId id, CancellationToken cancellationToken)
    {
        SessionKey key = new(user, id);
        Entry entry = Acquire(key);
        try
        {
            await entry.Lock.WaitAsync(cancellationToken);
            try
            {
                if (SessionOf(entry) is { } session)
                {
                    await session.CloseAsync(cancellationToken);
                    SetSession(entry, null);
                }
            }
            finally
            {
                entry.Lock.Release();
            }
        }
        finally
        {
            Release(key, entry);
        }
    }

    /// <summary>Deletes a project: closes its session without writing it and removes its file, under the project's lock so nothing reopens it halfway.</summary>
    /// <returns>False when the user had no such project.</returns>
    public async Task<bool> DeleteAsync(UserId user, ProjectId id, CancellationToken cancellationToken)
    {
        SessionKey key = new(user, id);
        Entry entry = Acquire(key);
        try
        {
            await entry.Lock.WaitAsync(cancellationToken);
            try
            {
                ProjectSession? open = SessionOf(entry);
                if (open is not null)
                {
                    await open.DiscardAsync(cancellationToken);
                    SetSession(entry, null);
                }
                return store.Delete(user, id) || open is not null;
            }
            finally
            {
                entry.Lock.Release();
            }
        }
        finally
        {
            Release(key, entry);
        }
    }

    /// <summary>Writes every session's unsaved changes, keeps the sessions, and returns how many could not be written.</summary>
    public async Task<int> FlushAllAsync(CancellationToken cancellationToken)
    {
        int failed = 0;
        foreach (ProjectSession session in AllSessions())
        {
            try
            {
                await session.SaveAsync(cancellationToken);
            }
            catch (SessionClosedException)
            {
                // Closed or deleted since the list was taken; a close writes what was unsaved before it ends the session.
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ProjectFormatException)
            {
                failed++;
                StoreLog.FlushFailed(logger, session.Id.Value, exception);
            }
        }
        return failed;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>Writes every unsaved change before the server stops, so the debounce never loses the last edit.</summary>
    public async Task StopAsync(CancellationToken cancellationToken) => await FlushAllAsync(cancellationToken);

    /// <summary>Closes every session, writing what it can.</summary>
    public async ValueTask DisposeAsync()
    {
        foreach (ProjectSession session in AllSessions())
        {
            try
            {
                await session.CloseAsync(CancellationToken.None);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ProjectFormatException)
            {
                StoreLog.FlushFailed(logger, session.Id.Value, exception);
                await session.DiscardAsync(CancellationToken.None);
            }
        }
    }

    // The session of an entry is read and written under the table's lock, the one lock every reader of it holds.
    private ProjectSession? SessionOf(Entry entry)
    {
        lock (_table)
        {
            return entry.Session;
        }
    }

    private void SetSession(Entry entry, ProjectSession? session)
    {
        lock (_table)
        {
            entry.Session = session;
        }
    }

    private List<ProjectSession> AllSessions()
    {
        lock (_table)
        {
            return [.. _entries.Values.Select(entry => entry.Session).OfType<ProjectSession>()];
        }
    }

    // An entry is counted while a caller holds or waits for it, so it is never dropped from under a waiter.
    private Entry Acquire(SessionKey key)
    {
        lock (_table)
        {
            if (!_entries.TryGetValue(key, out Entry? entry))
            {
                entry = new Entry();
                _entries[key] = entry;
            }
            entry.Users++;
            return entry;
        }
    }

    private void Release(SessionKey key, Entry entry)
    {
        lock (_table)
        {
            entry.Users--;
            if (entry.Users == 0 && entry.Session is null)
            {
                _entries.Remove(key);
            }
        }
    }

    private sealed class OrderLease(OpenProjects owner, SessionKey key, Entry entry) : IDisposable
    {
        private int _released;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0)
            {
                entry.Order.Release();
                owner.Release(key, entry);
            }
        }
    }

    private sealed class Entry
    {
        public SemaphoreSlim Lock { get; } = new(1, 1);

        public SemaphoreSlim Order { get; } = new(1, 1);

        public ProjectSession? Session { get; set; }

        public int Users { get; set; }
    }
}
