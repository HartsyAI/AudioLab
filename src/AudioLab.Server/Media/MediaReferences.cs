using AudioLab.Core.Model;
using AudioLab.Core.Rules;
using AudioLab.Server.Store;

namespace AudioLab.Server.Media;

/// <summary>Finds a project of a user that plays a media file: open ones with their whole undo history, and stored ones.</summary>
/// <remarks>
/// A stored project file that cannot be read is not counted: it can only be opened again after it is repaired, and opening
/// checks every media file it names, so it can never play a file that is gone.
/// </remarks>
public sealed class MediaReferences(OpenProjects open, ProjectStore projects)
{
    /// <summary>The name of a project of <paramref name="user"/> that plays <paramref name="media"/>, or null when none does.</summary>
    public async Task<string?> FindProjectAsync(UserId user, MediaId media, CancellationToken cancellationToken)
    {
        IReadOnlyList<ProjectSession> sessions = open.SessionsOf(user);
        foreach (ProjectSession session in sessions)
        {
            if (session.ReferencesMedia(media))
            {
                return session.Snapshot.Document.Name;
            }
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
            catch (Exception exception) when (exception is ProjectNotFoundException or ProjectFormatException)
            {
                // Deleted since the list was made, or unreadable: see the remarks.
            }
        }
        return null;
    }
}
