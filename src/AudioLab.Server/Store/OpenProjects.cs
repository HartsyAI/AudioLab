using AudioLab.Core.Model;

namespace AudioLab.Server.Store;

/// <summary>The open projects: one <see cref="ProjectSession"/> per project, so every client and agent edits the same document.</summary>
/// <remarks>
/// Each project has its own lock, so loading, closing or deleting one never waits on another; a short lock guards only
/// the table of locks. A session stays until it is closed, deleted or the server stops, because the hub will tie its
/// lifetime to its clients. A session never leaves the registry before its unsaved changes are on disk.
/// </remarks>
public sealed class OpenProjects(ProjectStore store, StoreOptions options, TimeProvider time, ILogger<OpenProjects> logger)
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
                if (entry.Session is null)
                {
                    ProjectDocument document = await store.ReadAsync(user, id, cancellationToken);
                    entry.Session = new ProjectSession(store, document, store.ModifiedAt(user, id), options, time, logger);
                }
                return entry.Session;
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

    /// <summary>The current snapshot of an open project, or null when it is not open; it never opens the project.</summary>
    public ProjectSnapshot? SnapshotIfOpen(UserId user, ProjectId id)
    {
        lock (_table)
        {
            return _entries.TryGetValue(new SessionKey(user, id), out Entry? entry) ? entry.Session?.Snapshot : null;
        }
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
                if (entry.Session is { } session)
                {
                    await session.CloseAsync(cancellationToken);
                    entry.Session = null;
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
                bool wasOpen = entry.Session is not null;
                if (entry.Session is { } session)
                {
                    await session.DiscardAsync(cancellationToken);
                    entry.Session = null;
                }
                return store.Delete(user, id) || wasOpen;
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

    private sealed class Entry
    {
        public SemaphoreSlim Lock { get; } = new(1, 1);

        public ProjectSession? Session { get; set; }

        public int Users { get; set; }
    }
}
