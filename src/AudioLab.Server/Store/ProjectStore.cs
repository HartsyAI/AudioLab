using System.Text;
using AudioLab.Core.Model;

namespace AudioLab.Server.Store;

/// <summary>Projects on disk, one folder per user. Reads are strict; writes are atomic.</summary>
/// <remarks>
/// A file is accepted for a user only if it passes <see cref="ProjectValidator"/>, is stored under the name of the project
/// it holds, and names that user as its owner. The owner is never taken from the file.
/// </remarks>
public sealed class ProjectStore(StoreRoot root, ILogger<ProjectStore> logger)
{
    private static readonly UTF8Encoding _strictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>The paths the store builds.</summary>
    public StorePaths Paths => root.Paths;

    /// <summary>Reads a user's project.</summary>
    /// <exception cref="ProjectNotFoundException">The user has no such project.</exception>
    /// <exception cref="ProjectFormatException">The file is not a valid project of this user; the problems are listed.</exception>
    public async Task<ProjectDocument> ReadAsync(UserId user, ProjectId id, CancellationToken cancellationToken)
    {
        string path = root.Paths.ProjectFile(user, id);
        ProjectDocument document = await ReadFileAsync(path, cancellationToken) ?? throw new ProjectNotFoundException();
        Verify(user, StorePaths.ProjectFileName(id), document);
        return document;
    }

    /// <summary>When a user's project file last changed.</summary>
    public DateTimeOffset ModifiedAt(UserId user, ProjectId id)
    {
        return new DateTimeOffset(File.GetLastWriteTimeUtc(root.Paths.ProjectFile(user, id)), TimeSpan.Zero);
    }

    /// <summary>Writes a project into its owner's folder, replacing the file atomically.</summary>
    /// <exception cref="ProjectFormatException">The document is not valid, or is larger than the store accepts.</exception>
    public async Task WriteAsync(ProjectDocument document, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        byte[] bytes = Encoding.UTF8.GetBytes(ProjectJson.Write(document));
        if (bytes.Length > root.Options.MaxFileBytes)
        {
            throw new ProjectFormatException("The project is larger than the store accepts.", ["file: the project is larger than the store accepts."]);
        }
        await root.Atomic.WriteAsync(root.Paths.ProjectFile(document.Owner, document.Id), bytes, cancellationToken);
    }

    /// <summary>Deletes a user's project file; false when there was none.</summary>
    public bool Delete(UserId user, ProjectId id)
    {
        string path = root.Paths.ProjectFile(user, id);
        if (!File.Exists(path))
        {
            return false;
        }
        File.Delete(path);
        return true;
    }

    /// <summary>Lists a user's valid projects, leaving out the files named in <paramref name="skipFiles"/> and counting the files it refuses.</summary>
    public async Task<ProjectListing> ListAsync(UserId user, IReadOnlySet<string> skipFiles, CancellationToken cancellationToken)
    {
        string directory = root.Paths.ProjectsDirectory(user);
        List<ProjectSummary> projects = [];
        int refused = 0;
        if (Directory.Exists(directory))
        {
            foreach (string file in Directory.EnumerateFiles(directory).Where(file => StorePaths.IsRecordFileName(Path.GetFileName(file))))
            {
                string name = Path.GetFileName(file);
                if (skipFiles.Contains(name))
                {
                    continue;
                }
                try
                {
                    ProjectDocument? document = await ReadFileAsync(file, cancellationToken);
                    if (document is not null)
                    {
                        Verify(user, name, document);
                        projects.Add(new ProjectSummary(document.Id.Value, document.Name, new DateTimeOffset(File.GetLastWriteTimeUtc(file), TimeSpan.Zero)));
                    }
                }
                catch (ProjectFormatException exception)
                {
                    refused++;
                    StoreLog.FileRefused(logger, name, string.Join("; ", exception.Problems));
                }
            }
        }
        return new ProjectListing(projects, refused);
    }

    // Null when the file does not exist.
    private async Task<ProjectDocument?> ReadFileAsync(string path, CancellationToken cancellationToken)
    {
        byte[] bytes;
        try
        {
            FileStreamOptions options = new()
            {
                Mode = FileMode.Open,
                Access = FileAccess.Read,
                Share = FileShare.ReadWrite | FileShare.Delete,
                Options = FileOptions.Asynchronous | FileOptions.SequentialScan,
            };
            await using FileStream stream = new(path, options);
            if (stream.Length > root.Options.MaxFileBytes)
            {
                throw new ProjectFormatException("The project file is larger than the store accepts.", ["file: the file is larger than the store accepts."]);
            }
            bytes = new byte[stream.Length];
            await stream.ReadExactlyAsync(bytes, cancellationToken);
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return null;
        }
        try
        {
            return ProjectJson.Read(_strictUtf8.GetString(bytes));
        }
        catch (DecoderFallbackException exception)
        {
            throw new ProjectFormatException("The project file is not valid UTF-8 text.", ["file: the file is not valid UTF-8 text."], exception);
        }
    }

    private static void Verify(UserId user, string expectedFileName, ProjectDocument document)
    {
        List<string> problems = [];
        if (document.Owner != user)
        {
            problems.Add("owner: the file names a different owner than the user it is stored for.");
        }
        if (StorePaths.ProjectFileName(document.Id) != expectedFileName)
        {
            problems.Add("id: the file is stored under the name of a different project.");
        }
        if (problems.Count > 0)
        {
            throw new ProjectFormatException("The stored project was refused:", problems);
        }
    }
}
