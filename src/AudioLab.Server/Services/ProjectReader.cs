using AudioLab.Core.Model;
using AudioLab.Server.Store;

namespace AudioLab.Server.Services;

/// <summary>Reads a project of a user without opening it for editing: what its open session holds now, else its stored file.</summary>
public sealed class ProjectReader(OpenProjects open, ProjectStore store)
{
    /// <summary>The project's current document, including changes not yet saved; another user's project is not found.</summary>
    /// <exception cref="ProjectNotFoundException">The user has no such project.</exception>
    /// <exception cref="ProjectFormatException">The stored file is not a valid project.</exception>
    /// <exception cref="IOException">The project store cannot be read.</exception>
    public async Task<ProjectDocument> CurrentAsync(UserId user, ProjectId project, CancellationToken cancellationToken)
    {
        return open.SnapshotIfOpen(user, project)?.Document ?? await store.ReadAsync(user, project, cancellationToken);
    }
}
