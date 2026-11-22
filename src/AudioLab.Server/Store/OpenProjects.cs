using AudioLab.Core.Model;

namespace AudioLab.Server.Store;

/// <summary>The open projects: one <see cref="ProjectSession"/> per project, so every client and agent edits the same document.</summary>
/// <remarks>
/// A session stays until it is closed, discarded or the server stops; the hub will tie that to its clients. A session
/// never leaves this registry before its unsaved changes are on disk. Loading a project holds the registry's lock.
/// </remarks>
public sealed class OpenProjects(ProjectStore store, StoreOptions options, TimeProvider time, ILogger<OpenProjects> logger)
    : IHostedService, IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Dictionary<SessionKey, ProjectSession> _sessions = [];

    /// <summary>Returns the project's session, loading and checking the project from disk if it is not open yet.</summary>
    /// <exception cref="ProjectNotFoundException">The user has no such project.</exception>
    /// <exception cref="ProjectFormatException">The stored file is not a valid project of this user.</exception>
    public async Task<ProjectSession> OpenAsync(UserId user, ProjectId id, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            SessionKey key = new(user, id);
            if (_sessions.TryGetValue(key, out ProjectSession? open))
            {
                return open;
            }
            ProjectDocument document = await store.ReadAsync(user, id, cancellationToken);
            ProjectSession session = new(store, document, store.ModifiedAt(user, id), options, time, logger);
            _sessions[key] = session;
            return session;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>The sessions a user has open.</summary>
    public async Task<IReadOnlyList<ProjectSession>> SessionsOfAsync(UserId user, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return [.. _sessions.Where(pair => pair.Key.User == user).Select(pair => pair.Value)];
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Writes a project's unsaved changes and closes its session; if the write fails the session stays open.</summary>
    public async Task CloseAsync(UserId user, ProjectId id, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            SessionKey key = new(user, id);
            if (_sessions.TryGetValue(key, out ProjectSession? session))
            {
                await session.CloseAsync(cancellationToken);
                _sessions.Remove(key);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Deletes a project: closes its session without writing it and removes its file, all under the registry's lock so nothing reopens it halfway.</summary>
    /// <returns>False when the user had no such project.</returns>
    public async Task<bool> DeleteAsync(UserId user, ProjectId id, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            SessionKey key = new(user, id);
            bool wasOpen = _sessions.TryGetValue(key, out ProjectSession? session);
            if (session is not null)
            {
                await session.DiscardAsync(cancellationToken);
                _sessions.Remove(key);
            }
            return store.Delete(user, id) || wasOpen;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Writes every session's unsaved changes, keeps the sessions, and returns how many could not be written.</summary>
    public async Task<int> FlushAllAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            int failed = 0;
            foreach (ProjectSession session in _sessions.Values)
            {
                try
                {
                    await session.SaveAsync(cancellationToken);
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ProjectFormatException)
                {
                    failed++;
                    StoreLog.FlushFailed(logger, session.Id.Value, exception);
                }
            }
            return failed;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>Writes every unsaved change before the server stops, so the debounce never loses the last edit.</summary>
    public async Task StopAsync(CancellationToken cancellationToken) => await FlushAllAsync(cancellationToken);

    /// <summary>Closes every session, writing what it can.</summary>
    public async ValueTask DisposeAsync()
    {
        await _gate.WaitAsync();
        try
        {
            foreach (ProjectSession session in _sessions.Values)
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
            _sessions.Clear();
        }
        finally
        {
            _gate.Release();
        }
    }
}
