using System.Collections.Concurrent;
using System.Text;
using AudioLab.Core.Model;
using AudioLab.Server.Store;

namespace AudioLab.Server.Media;

/// <summary>The media files of every user on disk, with an index in memory that the command pipeline can ask without waiting on the disk.</summary>
/// <remarks>
/// A file is a record (<c>m&lt;hash&gt;.json</c>) and its bytes (<c>m&lt;hash&gt;.wav</c>) in the owner's media folder, under the same
/// hashed names and path checks as projects. The bytes are renamed into place first and the record is written last, so a
/// record always has its bytes; bytes with no record are removed when the store starts. A user's folder is read the first
/// time the user is asked about (<see cref="PrepareAsync"/> does it off a request's thread), and after that the index is the
/// truth: one process at a time holds the store. The index of a user stays until the server stops.
/// </remarks>
public sealed class MediaStore : IMediaCatalog
{
    private const int MaxRecordBytes = 8192;

    private static readonly TimeSpan _retryAfter = TimeSpan.FromSeconds(5);

    private static readonly UTF8Encoding _strictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private readonly StoreRoot _root;
    private readonly IMediaQuota _quota;
    private readonly MediaOptions _options;
    private readonly TimeProvider _time;
    private readonly ILogger<MediaStore> _logger;
    private readonly ConcurrentDictionary<UserId, UserMedia> _users = new();

    /// <summary>Creates the store; it reads nothing until <see cref="SweepOrphans"/> or a user is first asked about.</summary>
    public MediaStore(StoreRoot root, IMediaQuota quota, MediaOptions options, TimeProvider time, ILogger<MediaStore> logger)
    {
        _root = root;
        _quota = quota;
        _options = options;
        _time = time;
        _logger = logger;
    }

    /// <inheritdoc />
    /// <remarks>While a user's folder cannot be read nothing is found, so a document is never accepted on a guess.</remarks>
    public MediaRecord? Find(UserId owner, MediaId id)
    {
        UserMedia media = Loaded(owner);
        lock (media.Sync)
        {
            return media.Entries.TryGetValue(id, out MediaEntry? entry) && !entry.PendingDelete ? entry.Record : null;
        }
    }

    /// <summary>Reads the user's media folder if that has not been done, off the caller's thread, so a later lookup does not wait.</summary>
    public Task PrepareAsync(UserId user)
    {
        UserMedia media = _users.GetOrAdd(user, _ => new UserMedia());
        lock (media.Sync)
        {
            if (media.Loaded && media.Unreadable.Count == 0)
            {
                return Task.CompletedTask;
            }
        }
        return Task.Run(() => Loaded(user));
    }

    /// <summary>A user's file, also while its delete is pending: only a new reference to it is refused, not a read.</summary>
    /// <exception cref="MediaNotFoundException">The user has no such file.</exception>
    /// <exception cref="IOException">The user's folder cannot be read.</exception>
    public MediaRecord Get(UserId user, MediaId id)
    {
        UserMedia media = Readable(user);
        lock (media.Sync)
        {
            return media.Entries.TryGetValue(id, out MediaEntry? entry) ? entry.Record : throw new MediaNotFoundException();
        }
    }

    /// <summary>A user's files, newest first, and how many stored records were refused as invalid.</summary>
    /// <exception cref="IOException">The user's folder cannot be read.</exception>
    public (IReadOnlyList<MediaRecord> Records, int Refused) List(UserId user)
    {
        UserMedia media = Readable(user);
        lock (media.Sync)
        {
            List<MediaRecord> records =
            [
                .. media.Entries.Values
                    .Select(entry => entry.Record)
                    .OrderByDescending(record => record.Created)
                    .ThenBy(record => record.Id.Value, StringComparer.Ordinal),
            ];
            return (records, media.Refused);
        }
    }

    /// <summary>What a user has stored, counting a file that is being deleted until it is gone.</summary>
    /// <exception cref="IOException">The user's folder cannot be read.</exception>
    public MediaUsage UsageOf(UserId user)
    {
        UserMedia media = Readable(user);
        lock (media.Sync)
        {
            return new MediaUsage(media.Entries.Count, media.TotalBytes);
        }
    }

    /// <summary>Starts an upload: it counts against the user's quota while it runs, and only a few may run at once.</summary>
    /// <exception cref="MediaBusyException">The user already has as many uploads running as are allowed.</exception>
    /// <exception cref="IOException">The user's folder cannot be read.</exception>
    public MediaUpload BeginUpload(UserId user)
    {
        UserMedia media = Readable(user);
        lock (media.Sync)
        {
            if (media.UploadsRunning >= _options.MaxConcurrentUploadsPerUser)
            {
                throw new MediaBusyException(_options.MaxConcurrentUploadsPerUser);
            }
            media.UploadsRunning++;
        }
        return new MediaUpload(this, user, media);
    }

    /// <summary>Stores a file: renames its bytes into place, then writes its record, under the user's lock so no race passes the quota.</summary>
    /// <param name="record">The record of the file, with the id the temporary file's name was made from.</param>
    /// <param name="tempData">The finished temporary file of the bytes; it is moved, so it must be on the same volume.</param>
    /// <param name="tempPeaks">The finished temporary file of the peaks, moved in the same way.</param>
    /// <param name="cancellationToken">Cancels the commit before the record is written.</param>
    /// <exception cref="MediaQuotaExceededException">The quota says no.</exception>
    /// <exception cref="IOException">The user's folder cannot be read or written.</exception>
    public async Task<MediaRecord> CommitAsync(MediaRecord record, string tempData, string tempPeaks, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);
        UserMedia media = Readable(record.Owner);
        byte[] json = Encoding.UTF8.GetBytes(MediaJson.Write(record));
        await media.Gate.WaitAsync(cancellationToken);
        try
        {
            MediaUsage stored;
            lock (media.Sync)
            {
                stored = new MediaUsage(media.Entries.Count, media.TotalBytes);
            }
            if (await _quota.RefusalAsync(record.Owner, stored, record.Bytes, cancellationToken) is { } refusal)
            {
                throw new MediaQuotaExceededException(refusal);
            }
            string data = _root.Paths.MediaDataFile(record.Owner, record.Id);
            string peaks = _root.Paths.MediaPeaksFile(record.Owner, record.Id);
            File.Move(tempData, data);
            try
            {
                File.Move(tempPeaks, peaks);
                await _root.Atomic.WriteAsync(_root.Paths.MediaRecordFile(record.Owner, record.Id), json, cancellationToken);
            }
            catch
            {
                // Whatever stopped the record, the bytes it would have belonged to must not stay; the error itself goes on.
                TryDelete(data);
                TryDelete(peaks);
                throw;
            }
            lock (media.Sync)
            {
                media.Entries[record.Id] = new MediaEntry(record);
                media.TotalBytes += record.Bytes;
            }
            return record;
        }
        finally
        {
            media.Gate.Release();
        }
    }

    /// <summary>Opens a user's file for reading; a delete in progress does not wait for the reader, and the reader keeps its bytes.</summary>
    /// <exception cref="MediaNotFoundException">The user has no such file.</exception>
    public FileStream OpenContent(UserId user, MediaId id)
    {
        MediaRecord record = Get(user, id);
        try
        {
            return OpenForReading(_root.Paths.MediaDataFile(user, record.Id));
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            throw new MediaNotFoundException();
        }
    }

    /// <inheritdoc />
    public MediaRecord? FindIncludingPendingDelete(UserId owner, MediaId id)
    {
        UserMedia media = Loaded(owner);
        lock (media.Sync)
        {
            return media.Entries.TryGetValue(id, out MediaEntry? entry) ? entry.Record : null;
        }
    }

    /// <summary>Opens the peaks of a user's file for reading.</summary>
    /// <exception cref="MediaNotFoundException">The user has no such file or the peak file is not there.</exception>
    public FileStream OpenPeaks(UserId user, MediaId id)
    {
        MediaRecord record = Get(user, id);
        try
        {
            return OpenForReading(_root.Paths.MediaPeaksFile(user, record.Id));
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            throw new MediaNotFoundException();
        }
    }

    /// <summary>Refuses a file from now on: its bytes were found missing or invalid, which is a damaged media file, not a damaged cache.</summary>
    public void Refuse(UserId user, MediaId id, string reason)
    {
        UserMedia media = Readable(user);
        lock (media.Sync)
        {
            if (media.Entries.Remove(id, out MediaEntry? removed))
            {
                media.TotalBytes -= removed.Record.Bytes;
                media.Refused++;
                MediaLog.RecordRefused(_logger, StorePaths.MediaFileName(id), reason);
            }
        }
    }

    /// <summary>The bytes files of every user that have a record but no peak file, as pairs of paths, for the rebuild at start.</summary>
    public IReadOnlyList<(string Data, string Peaks)> FindMissingPeaks()
    {
        List<(string, string)> missing = [];
        if (!Directory.Exists(_root.Paths.UsersDirectory))
        {
            return missing;
        }
        foreach (string userFolder in Directory.EnumerateDirectories(_root.Paths.UsersDirectory))
        {
            string media = Path.Combine(userFolder, "media");
            if (!Directory.Exists(media))
            {
                continue;
            }
            foreach (string file in Directory.EnumerateFiles(media, "*" + StorePaths.DataExtension))
            {
                string? record = StorePaths.RecordNameOfData(Path.GetFileName(file));
                string peaks = Path.ChangeExtension(file, StorePaths.PeaksExtension);
                if (record is not null && File.Exists(Path.Combine(media, record)) && !File.Exists(peaks))
                {
                    missing.Add((file, peaks));
                }
            }
        }
        return missing;
    }

    /// <summary>Hides a file from <see cref="Find"/> and from reads; the first step of a delete.</summary>
    /// <exception cref="MediaNotFoundException">The user has no such file.</exception>
    /// <exception cref="MediaDeleteInProgressException">Another delete of the file has begun and may still be refused.</exception>
    public MediaRecord BeginDelete(UserId user, MediaId id)
    {
        UserMedia media = Readable(user);
        lock (media.Sync)
        {
            if (!media.Entries.TryGetValue(id, out MediaEntry? entry))
            {
                throw new MediaNotFoundException();
            }
            if (entry.PendingDelete)
            {
                throw new MediaDeleteInProgressException();
            }
            entry.PendingDelete = true;
            return entry.Record;
        }
    }

    /// <summary>Makes a file visible again after <see cref="BeginDelete"/>, because the delete was refused.</summary>
    public void CancelDelete(UserId user, MediaId id)
    {
        UserMedia media = Readable(user);
        lock (media.Sync)
        {
            if (media.Entries.TryGetValue(id, out MediaEntry? entry))
            {
                entry.PendingDelete = false;
            }
        }
    }

    /// <summary>Removes a file whose delete was begun: its record first, so a crash leaves bytes to sweep and never a record without bytes.</summary>
    public async Task FinishDeleteAsync(UserId user, MediaId id, CancellationToken cancellationToken)
    {
        UserMedia media = Readable(user);
        await media.Gate.WaitAsync(cancellationToken);
        try
        {
            File.Delete(_root.Paths.MediaRecordFile(user, id));
            lock (media.Sync)
            {
                if (media.Entries.Remove(id, out MediaEntry? removed))
                {
                    media.TotalBytes -= removed.Record.Bytes;
                }
            }
            TryDelete(_root.Paths.MediaDataFile(user, id));
            TryDelete(_root.Paths.MediaPeaksFile(user, id));
        }
        finally
        {
            media.Gate.Release();
        }
    }

    internal static FileStream OpenForReading(string path)
    {
        FileStreamOptions options = new()
        {
            Mode = FileMode.Open,
            Access = FileAccess.Read,
            Share = FileShare.ReadWrite | FileShare.Delete,
            Options = FileOptions.Asynchronous,
        };
        return new FileStream(path, options);
    }

    // Called by MediaUpload: what the user has, plus what their other uploads have received, plus what this one has.
    internal async ValueTask<string?> UploadRefusalAsync(
        UserId user,
        UserMedia media,
        long ownBytes,
        long incoming,
        CancellationToken cancellationToken)
    {
        MediaUsage usage;
        lock (media.Sync)
        {
            usage = new MediaUsage(media.Entries.Count + media.UploadsRunning - 1, media.TotalBytes + media.UploadBytes - ownBytes);
        }
        return await _quota.RefusalAsync(user, usage, incoming, cancellationToken);
    }

    // Fails closed: a folder that cannot be read gives an index with nothing in it, which finds nothing.
    private UserMedia Loaded(UserId user)
    {
        UserMedia media = _users.GetOrAdd(user, _ => new UserMedia());
        lock (media.Sync)
        {
            if (!media.Loaded)
            {
                media.Loaded = Load(user, media);
                media.LastAttempt = _time.GetUtcNow();
            }
            else if (media.Unreadable.Count > 0 && _time.GetUtcNow() - media.LastAttempt >= _retryAfter)
            {
                RetryUnreadable(user, media);
            }
        }
        return media;
    }

    private UserMedia Readable(UserId user)
    {
        UserMedia media = Loaded(user);
        lock (media.Sync)
        {
            return media.Loaded ? media : throw new IOException("The user's media folder cannot be read.");
        }
    }

    // The folder is read into a list of its own and merged only when all of it was listed, so a failure part way changes nothing.
    private bool Load(UserId user, UserMedia media)
    {
        string directory = _root.Paths.MediaDirectory(user);
        List<MediaEntry> found = [];
        List<string> unreadable = [];
        int refused = 0;
        try
        {
            if (Directory.Exists(directory))
            {
                foreach (string file in Directory.EnumerateFiles(directory).Where(file => StorePaths.IsRecordFileName(Path.GetFileName(file))))
                {
                    if (TryLoadRecord(user, file, unreadable) is { } entry)
                    {
                        found.Add(entry);
                    }
                    else
                    {
                        refused++;
                    }
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            MediaLog.FolderUnreadable(_logger, exception.GetType().Name);
            return false;
        }
        foreach (MediaEntry entry in found)
        {
            if (media.Entries.TryAdd(entry.Record.Id, entry))
            {
                media.TotalBytes += entry.Record.Bytes;
            }
        }
        media.Refused += refused;
        media.Unreadable.AddRange(unreadable);
        return true;
    }

    // A record another process held is tried again, so a passing lock by a scanner is not permanent.
    private void RetryUnreadable(UserId user, UserMedia media)
    {
        string[] files = [.. media.Unreadable];
        media.Unreadable.Clear();
        media.LastAttempt = _time.GetUtcNow();
        foreach (string file in files)
        {
            if (TryLoadRecord(user, file, media.Unreadable) is { } entry)
            {
                media.Refused--;
                if (media.Entries.TryAdd(entry.Record.Id, entry))
                {
                    media.TotalBytes += entry.Record.Bytes;
                }
            }
        }
    }

    // Null for a record that is refused, which is logged once: a bad record never hides the others.
    private MediaEntry? TryLoadRecord(UserId user, string file, List<string> unreadable)
    {
        string name = Path.GetFileName(file);
        try
        {
            MediaRecord record = ReadRecord(file);
            List<string> problems = Problems(user, name, record);
            if (problems.Count == 0)
            {
                return new MediaEntry(record);
            }
            MediaLog.RecordRefused(_logger, name, string.Join("; ", problems));
        }
        catch (ProjectFormatException exception)
        {
            MediaLog.RecordRefused(_logger, name, string.Join("; ", exception.Problems));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            unreadable.Add(file);
            MediaLog.RecordRefused(_logger, name, exception.GetType().Name);
        }
        return null;
    }

    private static MediaRecord ReadRecord(string path)
    {
        FileStreamOptions options = new() { Mode = FileMode.Open, Access = FileAccess.Read, Share = FileShare.ReadWrite | FileShare.Delete };
        using FileStream stream = new(path, options);
        if (stream.Length > MaxRecordBytes)
        {
            string problem = "file: the file is larger than a record can be.";
            throw new ProjectFormatException("The media record file is larger than a record can be.", [problem]);
        }
        byte[] bytes = new byte[stream.Length];
        stream.ReadExactly(bytes);
        try
        {
            return MediaJson.Read(_strictUtf8.GetString(bytes));
        }
        catch (DecoderFallbackException exception)
        {
            throw new ProjectFormatException("The media record is not valid UTF-8 text.", ["file: the file is not valid UTF-8 text."], exception);
        }
    }

    private List<string> Problems(UserId user, string recordFileName, MediaRecord record)
    {
        List<string> problems = [];
        if (record.Owner != user)
        {
            problems.Add("owner: the file names a different owner than the user it is stored for.");
        }
        if (StorePaths.MediaFileName(record.Id) != recordFileName)
        {
            problems.Add("id: the file is stored under the name of a different record.");
        }
        if (problems.Count == 0)
        {
            FileInfo data = new(_root.Paths.MediaDataFile(user, record.Id));
            if (!data.Exists || data.Length != record.Bytes)
            {
                problems.Add("bytes: the media file is missing or is not the size its record says.");
            }
        }
        return problems;
    }

    /// <summary>Removes the bytes that have no record, which only a crash leaves; the host does it when it starts.</summary>
    /// <returns>How many files were removed.</returns>
    public int SweepOrphans()
    {
        int removed = 0;
        if (!Directory.Exists(_root.Paths.UsersDirectory))
        {
            return removed;
        }
        foreach (string userFolder in Directory.EnumerateDirectories(_root.Paths.UsersDirectory))
        {
            string media = Path.Combine(userFolder, "media");
            if (!Directory.Exists(media))
            {
                continue;
            }
            foreach (string file in Directory.EnumerateFiles(media))
            {
                string? record = StorePaths.RecordNameOfData(Path.GetFileName(file));
                if (record is not null && !File.Exists(Path.Combine(media, record)) && TryDelete(file))
                {
                    removed++;
                }
            }
        }
        MediaLog.OrphansSwept(_logger, removed);
        return removed;
    }

    private bool TryDelete(string path)
    {
        try
        {
            File.Delete(path);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            MediaLog.NotRemoved(_logger, Path.GetFileName(path), exception.GetType().Name);
            return false;
        }
    }
}
