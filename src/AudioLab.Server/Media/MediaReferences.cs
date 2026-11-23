using AudioLab.Core.Api;
using AudioLab.Core.Model;
using AudioLab.Core.Rules;
using AudioLab.Server.Store;

namespace AudioLab.Server.Media;

/// <summary>Finds a project of a user that plays a media file: open ones with their whole undo history, and stored ones.</summary>
/// <remarks>
/// A stored project file that cannot be read, because it is damaged or because another process holds it, is logged and not
/// counted, so it does not fail every delete. The search reads every stored project of the user whole, one at a time, so
/// its cost grows with the size of their projects (at most 1000 files of 32 MiB each by default).
/// </remarks>
public sealed class MediaReferences(
    OpenProjects open,
    ProjectStore projects,
    ILogger<MediaReferences> logger,
    Func<Task>? betweenPasses = null)
{
    /// <summary>The name of a project of <paramref name="user"/> that plays <paramref name="media"/>, or null when none does.</summary>
    public async Task<string?> FindProjectAsync(UserId user, MediaId media, CancellationToken cancellationToken)
    {
        IReadOnlyList<ProjectSession> sessions = open.SessionsOf(user);
        if (SessionReferencing(sessions, media) is { } inSession)
        {
            return inSession;
        }
        if (betweenPasses is not null)
        {
            await betweenPasses();
        }
        HashSet<string> openFiles = [.. sessions.Select(session => StorePaths.ProjectFileName(session.Id))];
        ProjectListing stored = await projects.ListAsync(user, openFiles, cancellationToken);
        foreach (ProjectSummary summary in stored.Projects)
        {
            try
            {
                ProjectDocument document = await projects.ReadAsync(user, ProjectId.From(summary.Id), cancellationToken);
                if (ProjectContent.ReferencedMedia(document).Contains(media))
                {
                    return document.Name;
                }
            }
            catch (ProjectNotFoundException)
            {
                // Deleted since the list was made.
            }
            catch (Exception exception) when (exception is ProjectFormatException or IOException or UnauthorizedAccessException)
            {
                MediaLog.ProjectNotSearched(logger, StorePaths.ProjectFileName(ProjectId.From(summary.Id)), exception.GetType().Name);
            }
        }

        // A session that opened while the stored files were read was not there for the first look; one that names the file is
        // refused when it opens, but this asks again so that nothing a session holds is ever missed.
        return SessionReferencing(open.SessionsOf(user), media);
    }

    private static string? SessionReferencing(IReadOnlyList<ProjectSession> sessions, MediaId media)
    {
        return sessions.FirstOrDefault(session => session.ReferencesMedia(media))?.Snapshot.Document.Name;
    }
}
