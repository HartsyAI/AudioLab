using System.Text;
using AudioLab.Core.Api;
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
        try
        {
            ProjectDocument document = await ReadFileAsync(path, cancellationToken) ?? throw new ProjectNotFoundException();
            Verify(user, StorePaths.ProjectFileName(id), document);
            return document;
        }
        catch (ProjectFormatException exception)
        {
            StoreLog.FileRefused(logger, StorePaths.ProjectFileName(id), string.Join("; ", exception.Problems));
            throw;
        }
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

    /// <summary>How many projects the user has stored.</summary>
    public int CountProjects(UserId user)
    {
        string directory = root.Paths.ProjectsDirectory(user);
        return Directory.Exists(directory) ? Directory.EnumerateFiles(directory).Count(file => StorePaths.IsRecordFileName(Path.GetFileName(file))) : 0;
    }

    /// <summary>The options the store runs with.</summary>
    public StoreOptions Options => root.Options;

    /// <summary>Lists a user's projects from the start of each file, leaving out the files named in <paramref name="skipFiles"/>.</summary>
    /// <remarks>
    /// The list reads only the first bytes of a file: the id, owner and name, and the modified time. Opening a project
    /// is what validates the whole file, so a project with a damaged body is listed and refuses to open. A file whose
    /// start cannot be read, whose owner is not the user or whose name is not its id's is counted as refused, and a
    /// file that cannot be read at all is skipped the same way, so one bad file never fails the list.
    /// </remarks>
    public async Task<ProjectListing> ListAsync(UserId user, IReadOnlySet<string> skipFiles, CancellationToken cancellationToken)
    {
        string directory = root.Paths.ProjectsDirectory(user);
        List<ProjectSummary> projects = [];
        int refused = 0;
        if (!Directory.Exists(directory))
        {
            return new ProjectListing(projects, refused);
        }
        foreach (string file in Directory.EnumerateFiles(directory).Where(file => StorePaths.IsRecordFileName(Path.GetFileName(file))))
        {
            string name = Path.GetFileName(file);
            if (skipFiles.Contains(name))
            {
                continue;
            }
            try
            {
                byte[]? prefix = await ReadPrefixAsync(file, cancellationToken);
                if (prefix is null)
                {
                    continue;
                }
                string? problem = ProjectHeader.TryRead(prefix, out ProjectHeader? header) ? HeaderProblem(user, name, header!) : "the file does not start with a project id, owner and name";
                if (problem is null)
                {
                    projects.Add(new ProjectSummary(header!.Id.Value, header.Name, new DateTimeOffset(File.GetLastWriteTimeUtc(file), TimeSpan.Zero)));
                }
                else
                {
                    refused++;
                    StoreLog.FileRefused(logger, name, problem);
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                refused++;
                StoreLog.IoFailed(logger, name, exception);
            }
        }
        return new ProjectListing(projects, refused);
    }

    private static async Task<byte[]?> ReadPrefixAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            FileStreamOptions options = new()
            {
                Mode = FileMode.Open,
                Access = FileAccess.Read,
                Share = FileShare.ReadWrite | FileShare.Delete,
                Options = FileOptions.Asynchronous,
            };
            await using FileStream stream = new(path, options);
            byte[] buffer = new byte[ProjectHeader.PrefixBytes];
            int read = await stream.ReadAtLeastAsync(buffer, buffer.Length, throwOnEndOfStream: false, cancellationToken);
            return buffer[..read];
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return null;
        }
    }

    private static string? HeaderProblem(UserId user, string expectedFileName, ProjectHeader header)
    {
        if (header.Owner != user)
        {
            return "owner: the file names a different owner than the user it is stored for";
        }
        return StorePaths.ProjectFileName(header.Id) == expectedFileName ? null : "id: the file is stored under the name of a different project";
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
