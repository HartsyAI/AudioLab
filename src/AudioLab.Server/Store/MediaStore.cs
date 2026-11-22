using System.Text;
using AudioLab.Core.Model;

namespace AudioLab.Server.Store;

/// <summary>Media records on disk, one folder per user. The bytes of a media file are not stored here yet; a record says what they will be.</summary>
/// <remarks>The owner of a record is always the user who creates it, and a read for another user finds nothing.</remarks>
public sealed class MediaStore(StoreRoot root, IIdService ids)
{
    private const int MaxRecordBytes = 4096;

    private static readonly UTF8Encoding _strictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>Creates a record owned by <paramref name="owner"/> with a new id.</summary>
    /// <exception cref="ProjectFormatException">The content type or length breaks the media rules.</exception>
    public async Task<MediaRecord> CreateAsync(UserId owner, string contentType, long length, CancellationToken cancellationToken)
    {
        MediaRecord record = new() { Id = ids.Next<MediaId>(), Owner = owner, ContentType = contentType, Length = length };
        byte[] bytes = Encoding.UTF8.GetBytes(MediaJson.Write(record));
        await root.Atomic.WriteAsync(root.Paths.MediaRecordFile(owner, record.Id), bytes, cancellationToken);
        return record;
    }

    /// <summary>Reads a user's record.</summary>
    /// <exception cref="ProjectNotFoundException">The user has no such record.</exception>
    /// <exception cref="ProjectFormatException">The file is not a valid record of this user.</exception>
    public async Task<MediaRecord> GetAsync(UserId user, MediaId id, CancellationToken cancellationToken)
    {
        string path = root.Paths.MediaRecordFile(user, id);
        MediaRecord record = await ReadFileAsync(path, cancellationToken) ?? throw new ProjectNotFoundException();
        Verify(user, StorePaths.MediaFileName(id), record);
        return record;
    }

    /// <summary>Lists a user's valid records and counts the files it refuses.</summary>
    public async Task<(IReadOnlyList<MediaRecord> Records, int Refused)> ListAsync(UserId user, CancellationToken cancellationToken)
    {
        string directory = root.Paths.MediaDirectory(user);
        List<MediaRecord> records = [];
        int refused = 0;
        if (Directory.Exists(directory))
        {
            foreach (string file in Directory.EnumerateFiles(directory).Where(file => StorePaths.IsRecordFileName(Path.GetFileName(file))))
            {
                try
                {
                    MediaRecord? record = await ReadFileAsync(file, cancellationToken);
                    if (record is not null)
                    {
                        Verify(user, Path.GetFileName(file), record);
                        records.Add(record);
                    }
                }
                catch (ProjectFormatException)
                {
                    refused++;
                }
            }
        }
        return (records, refused);
    }

    /// <summary>Deletes a user's record; false when there was none.</summary>
    public bool Delete(UserId user, MediaId id)
    {
        string path = root.Paths.MediaRecordFile(user, id);
        if (!File.Exists(path))
        {
            return false;
        }
        File.Delete(path);
        return true;
    }

    private static async Task<MediaRecord?> ReadFileAsync(string path, CancellationToken cancellationToken)
    {
        byte[] bytes;
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
            if (stream.Length > MaxRecordBytes)
            {
                throw new ProjectFormatException("The media record file is larger than a record can be.", ["file: the file is larger than a record can be."]);
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
            return MediaJson.Read(_strictUtf8.GetString(bytes));
        }
        catch (DecoderFallbackException exception)
        {
            throw new ProjectFormatException("The media record is not valid UTF-8 text.", ["file: the file is not valid UTF-8 text."], exception);
        }
    }

    private static void Verify(UserId user, string expectedFileName, MediaRecord record)
    {
        List<string> problems = [];
        if (record.Owner != user)
        {
            problems.Add("owner: the file names a different owner than the user it is stored for.");
        }
        if (StorePaths.MediaFileName(record.Id) != expectedFileName)
        {
            problems.Add("id: the file is stored under the name of a different record.");
        }
        if (problems.Count > 0)
        {
            throw new ProjectFormatException("The stored media record was refused:", problems);
        }
    }
}
