using System.Globalization;
using AudioLab.Core.Api;
using AudioLab.Core.Model;
using AudioLab.Server.Store;

namespace AudioLab.Server.Services;

/// <summary>Everything a user can do with their projects. Every method takes the user from the door; none reads an owner from a request.</summary>
public sealed class ProjectService(ProjectStore store, OpenProjects open, IIdService ids, TimeProvider time, IProjectEvents events)
{
    private readonly SemaphoreSlim _creating = new(1, 1);

    private const string CopyPrefix = "Copy of ";

    /// <summary>The user's projects, newest first, with how many stored files were left out as invalid.</summary>
    public async Task<ProjectListing> ListAsync(UserId user, CancellationToken cancellationToken)
    {
        IReadOnlyList<ProjectSession> sessions = open.SessionsOf(user);
        HashSet<string> openFiles = [.. sessions.Select(session => StorePaths.ProjectFileName(session.Id))];
        ProjectListing stored = await store.ListAsync(user, openFiles, cancellationToken);
        IEnumerable<ProjectSummary> all = stored.Projects.Concat(sessions.Select(session => session.Summary));
        List<ProjectSummary> ordered = [.. all.OrderByDescending(project => project.Modified).ThenBy(project => project.Name, StringComparer.Ordinal).ThenBy(project => project.Id, StringComparer.Ordinal)];
        return new ProjectListing(ordered, stored.Refused);
    }

    /// <summary>Creates an empty project owned by <paramref name="user"/>.</summary>
    /// <exception cref="ProjectFormatException">The name is missing or breaks the project rules.</exception>
    /// <exception cref="QuotaExceededException">The user already has the most projects allowed.</exception>
    public async Task<ProjectSummary> CreateAsync(UserId user, string? name, CancellationToken cancellationToken)
    {
        if (name is null)
        {
            throw new ProjectFormatException("The project is not valid.", ["name: a project needs a name."]);
        }
        ProjectDocument document = NewProjectFactory.Create(ids.Next<ProjectId>(), user, name);
        await WriteNewAsync(document, cancellationToken);
        return new ProjectSummary(document.Id.Value, document.Name, time.GetUtcNow());
    }

    /// <summary>Opens the project, so every client of it shares one document, and returns its current state.</summary>
    /// <exception cref="ProjectNotFoundException">The user has no such project.</exception>
    /// <exception cref="ProjectFormatException">The stored file is not a valid project; the problems are listed.</exception>
    public async Task<ProjectSnapshot> OpenAsync(UserId user, ProjectId id, CancellationToken cancellationToken)
    {
        ProjectSession session = await open.OpenAsync(user, id, cancellationToken);
        return session.Snapshot;
    }

    /// <summary>Writes the project's unsaved changes to disk now.</summary>
    public async Task<SaveResult> SaveAsync(UserId user, ProjectId id, CancellationToken cancellationToken)
    {
        ProjectSession session = await open.OpenAsync(user, id, cancellationToken);
        long revision = await session.SaveAsync(cancellationToken);
        return new SaveResult(id.Value, revision);
    }

    /// <summary>Deletes the project and closes it for everyone who has it open.</summary>
    /// <exception cref="ProjectNotFoundException">The user has no such project.</exception>
    public async Task DeleteAsync(UserId user, ProjectId id, CancellationToken cancellationToken)
    {
        // The project's publish order is taken even when it is not open: a join cannot then open it beside the deletion, and the
        // deletion is announced after any change that is being announced and before any join that follows.
        using IDisposable order = await open.EnterOrderAsync(user, id, cancellationToken);
        if (!await open.DeleteAsync(user, id, cancellationToken))
        {
            throw new ProjectNotFoundException();
        }
        await events.DeletedAsync(user, id, CancellationToken.None);
    }

    /// <summary>Saves a copy of the project, including its unsaved changes, as a new project of the same user; it does not open the original.</summary>
    /// <exception cref="ProjectNotFoundException">The user has no such project.</exception>
    /// <exception cref="QuotaExceededException">The user already has the most projects allowed.</exception>
    public async Task<ProjectSummary> DuplicateAsync(UserId user, ProjectId id, string? name, CancellationToken cancellationToken)
    {
        ProjectDocument original = open.SnapshotIfOpen(user, id)?.Document ?? await store.ReadAsync(user, id, cancellationToken);
        await open.RequireMediaAsync(original, cancellationToken);
        ProjectDocument copy = original with { Id = ids.Next<ProjectId>(), Name = name ?? CopyName(original.Name) };
        await WriteNewAsync(copy, cancellationToken);
        return new ProjectSummary(copy.Id.Value, copy.Name, time.GetUtcNow());
    }

    // One at a time, so two requests cannot both pass the count and go over the limit.
    private async Task WriteNewAsync(ProjectDocument document, CancellationToken cancellationToken)
    {
        await _creating.WaitAsync(cancellationToken);
        try
        {
            if (store.CountProjects(document.Owner) >= store.Options.MaxProjectsPerUser)
            {
                throw new QuotaExceededException(store.Options.MaxProjectsPerUser);
            }
            await store.WriteAsync(document, cancellationToken);
        }
        finally
        {
            _creating.Release();
        }
    }

    // Cut between text elements, so a long name never ends in half a character.
    private static string CopyName(string name)
    {
        string full = CopyPrefix + name;
        if (full.Length <= ProjectLimits.MaxName)
        {
            return full;
        }
        TextElementEnumerator elements = StringInfo.GetTextElementEnumerator(full);
        int end = 0;
        while (elements.MoveNext() && elements.ElementIndex + ((string)elements.Current).Length <= ProjectLimits.MaxName)
        {
            end = elements.ElementIndex + ((string)elements.Current).Length;
        }
        return full[..end];
    }
}
