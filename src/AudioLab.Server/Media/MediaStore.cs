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
    private enum ReadOutcome
    {
        Ok,
        Invalid,
        Unreadable,
        Gone,
    }

    private const int MaxRecordBytes = 8192;

    private static readonly TimeSpan[] _retryDelays = [TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(45)];

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
    /// <remarks>Never touches the disk: a user whose folder has not been read, or cannot be, finds nothing.</remarks>
    public MediaRecord? Find(UserId owner, MediaId id)
    {
        if (LoadedOnly(owner) is not { } media)
        {
            return null;
        }
        lock (media.Sync)
        {
            return media.Entries.TryGetValue(id, out MediaEntry? entry) && !entry.PendingDelete ? entry.Record : null;
        }
    }

    /// <summary>Reads the user's media folder if that has not been done, and reads again records whose time has come.</summary>
    /// <remarks>
    /// Off the caller's thread and out of the index's lock, so a lookup never waits for the disk. Once everything is read and no
    /// retry is due it costs nothing. A record that cannot be read is tried again after 5, 15 and 45 seconds and then
    /// stays unreadable; it counts against the quota all the while.
    /// </remarks>
    public Task PrepareAsync(UserId user, CancellationToken cancellationToken = default)
    {
        UserMedia media = _users.GetOrAdd(user, _ => new UserMedia());
        DateTimeOffset now = _time.GetUtcNow();
        lock (media.Sync)
        {
            bool nothingToDo = media.Loaded
                ? !media.Unreadable.Any(record => !record.Terminal && record.NextAttempt <= now)
                : now < media.NextLoadAttempt;
            if (nothingToDo)
            {
                return Task.CompletedTask;
            }
        }
        return Task.Run(
            () =>
            {
                Loaded(user);
                RetryDue(user, media);
            },
            cancellationToken);
    }

    /// <summary>Throws when the user's folder cannot be read, so a caller answers with a store failure and not with "not found".</summary>
    /// <exception cref="IOException">The user's folder cannot be read.</exception>
    public void RequireReadable(UserId user) => _ = Readable(user);

    /// <summary>Whether a delete of the file has begun and may still be refused; like <see cref="Find"/> it never touches the disk.</summary>
    public bool IsDeleting(UserId owner, MediaId id)
    {
        if (LoadedOnly(owner) is not { } media)
        {
            return false;
        }
        lock (media.Sync)
        {
            return media.Entries.TryGetValue(id, out MediaEntry? entry) && entry.PendingDelete;
        }
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
            return (records, media.Refused + media.Unreadable.Count);
        }
    }

    /// <summary>What a user has stored, counting a file that is being deleted, and a record that cannot be read, until it is gone.</summary>
    /// <exception cref="IOException">The user's folder cannot be read.</exception>
    public MediaUsage UsageOf(UserId user)
    {
        UserMedia media = Readable(user);
        lock (media.Sync)
        {
            return UsageOf(media);
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
                stored = UsageOf(media);
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
    /// <param name="user">The owner.</param>
    /// <param name="id">The file.</param>
    /// <param name="asynchronous">Whether the stream is for asynchronous reads, as a response is; a caller that reads synchronously passes false.</param>
    /// <exception cref="MediaNotFoundException">The user has no such file.</exception>
    public FileStream OpenContent(UserId user, MediaId id, bool asynchronous = true)
    {
        MediaRecord record = Get(user, id);
        try
        {
            return OpenForReading(_root.Paths.MediaDataFile(user, record.Id), asynchronous);
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
    /// <remarks>The record file gets the suffix <c>.refused</c>, so a restart does not bring the file back; the owner may restore it.</remarks>
    public void Refuse(UserId user, MediaId id, string reason)
    {
        UserMedia media = Readable(user);
        lock (media.Sync)
        {
            if (!media.Entries.Remove(id, out MediaEntry? removed))
            {
                return;
            }
            media.TotalBytes -= removed.Record.Bytes;
            media.Refused++;
        }
        MediaLog.RecordRefused(_logger, StorePaths.MediaFileName(id), reason);
        string record = _root.Paths.MediaRecordFile(user, id);
        try
        {
            File.Move(record, record + StorePaths.RefusedSuffix, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            MediaLog.NotRemoved(_logger, Path.GetFileName(record), exception.GetType().Name);
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

    /// <summary>Hides a file from <see cref="Find"/> (new references) while reads still see it; the first step of a delete.</summary>
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

    internal static FileStream OpenForReading(string path, bool asynchronous = true)
    {
        FileStreamOptions options = new()
        {
            Mode = FileMode.Open,
            Access = FileAccess.Read,
            Share = FileShare.ReadWrite | FileShare.Delete,
            Options = asynchronous ? FileOptions.Asynchronous : FileOptions.SequentialScan,
        };
        return new FileStream(path, options);
    }

    // Called by MediaUpload: what the user has, plus what their other admitted uploads have received; an upload is
    // counted only once it was admitted, so one that is about to be refused takes nothing from the others.
    internal async ValueTask<string?> UploadRefusalAsync(
        UserId user,
        UserMedia media,
        bool admitted,
        long ownBytes,
        long incoming,
        CancellationToken cancellationToken)
    {
        MediaUsage usage;
        lock (media.Sync)
        {
            MediaUsage stored = UsageOf(media);
            usage = new MediaUsage(stored.Files + media.UploadsAdmitted - (admitted ? 1 : 0), stored.Bytes + media.UploadBytes - ownBytes);
        }
        return await _quota.RefusalAsync(user, usage, incoming, cancellationToken);
    }

    // The folder is read without holding the index's lock; a lookup waits for nothing but the short merge.
    private UserMedia Loaded(UserId user)
    {
        UserMedia media = _users.GetOrAdd(user, _ => new UserMedia());
        lock (media.LoadSync)
        {
            bool read;
            lock (media.Sync)
            {
                read = media.Loaded || _time.GetUtcNow() < media.NextLoadAttempt;
            }
            if (!read)
            {
                Load(user, media);
            }
        }
        return media;
    }

    private UserMedia? LoadedOnly(UserId user)
    {
        if (!_users.TryGetValue(user, out UserMedia? media))
        {
            return null;
        }
        lock (media.Sync)
        {
            return media.Loaded ? media : null;
        }
    }

    private UserMedia Readable(UserId user)
    {
        UserMedia media = Loaded(user);
        lock (media.Sync)
        {
            return media.Loaded ? media : throw new IOException("The user's media folder cannot be read.");
        }
    }

    private static MediaUsage UsageOf(UserMedia media)
    {
        return new MediaUsage(media.Entries.Count + media.Unreadable.Count, media.TotalBytes + media.Unreadable.Sum(record => record.Bytes));
    }

    // All of the folder is listed and read first, and merged only when none of the listing failed, so a failure part way changes nothing.
    private void Load(UserId user, UserMedia media)
    {
        string directory = _root.Paths.MediaDirectory(user);
        List<MediaEntry> found = [];
        List<UnreadableRecord> unreadable = [];
        int refused = 0;
        try
        {
            if (Directory.Exists(directory))
            {
                foreach (string file in Directory.EnumerateFiles(directory))
                {
                    string name = Path.GetFileName(file);
                    if (StorePaths.IsRefusedFileName(name))
                    {
                        refused++;
                    }
                    else if (StorePaths.IsRecordFileName(name))
                    {
                        (ReadOutcome outcome, MediaEntry? entry) = TryRead(user, file, log: true);
                        switch (outcome)
                        {
                            case ReadOutcome.Ok:
                                found.Add(entry!);
                                break;
                            case ReadOutcome.Invalid:
                                refused++;
                                break;
                            case ReadOutcome.Unreadable:
                                unreadable.Add(NewUnreadable(file));
                                break;
                        }
                    }
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            MediaLog.FolderUnreadable(_logger, exception.GetType().Name);
            lock (media.Sync)
            {
                media.NextLoadAttempt = _time.GetUtcNow() + _retryDelays[0];
            }
            return;
        }
        lock (media.Sync)
        {
            foreach (MediaEntry entry in found)
            {
                if (media.Entries.TryAdd(entry.Record.Id, entry))
                {
                    media.TotalBytes += entry.Record.Bytes;
                }
            }
            media.Refused += refused;
            media.Unreadable.AddRange(unreadable);
            media.Loaded = true;
        }
    }

    // A record another process held is tried again at growing intervals and then given up on, so a passing lock by a scanner is not
    // permanent and a lasting one does not fill the log; the record counts against the quota until it can be read.
    private void RetryDue(UserId user, UserMedia media)
    {
        lock (media.LoadSync)
        {
            DateTimeOffset now = _time.GetUtcNow();
            UnreadableRecord[] due;
            lock (media.Sync)
            {
                due = [.. media.Unreadable.Where(record => !record.Terminal && record.NextAttempt <= now)];
            }
            foreach (UnreadableRecord record in due)
            {
                (ReadOutcome outcome, MediaEntry? entry) = TryRead(user, record.Path, log: false);
                lock (media.Sync)
                {
                    if (outcome == ReadOutcome.Unreadable)
                    {
                        record.Attempts++;
                        record.Terminal = record.Attempts > _retryDelays.Length;
                        record.NextAttempt = record.Terminal ? DateTimeOffset.MaxValue : now + _retryDelays[record.Attempts - 1];
                        if (record.Terminal)
                        {
                            MediaLog.RecordGivenUp(_logger, Path.GetFileName(record.Path));
                        }
                        continue;
                    }
                    media.Unreadable.Remove(record);
                    if (outcome == ReadOutcome.Ok && media.Entries.TryAdd(entry!.Record.Id, entry))
                    {
                        media.TotalBytes += entry.Record.Bytes;
                    }
                    else if (outcome == ReadOutcome.Invalid)
                    {
                        media.Refused++;
                    }
                }
            }
        }
    }

    private UnreadableRecord NewUnreadable(string recordFile)
    {
        FileInfo data = new(Path.ChangeExtension(recordFile, StorePaths.DataExtension));
        long bytes = 0;
        try
        {
            bytes = data.Exists ? data.Length : 0;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The size is only for the quota: an unknown size counts as none and the file still counts as a file.
            MediaLog.NotRemoved(_logger, data.Name, exception.GetType().Name);
        }
        return new UnreadableRecord(recordFile, bytes, _time.GetUtcNow() + _retryDelays[0]);
    }

    // A record that is not there any more is Gone and is dropped; one that is refused is logged once; one that cannot be read is retried.
    private (ReadOutcome Outcome, MediaEntry? Entry) TryRead(UserId user, string file, bool log)
    {
        string name = Path.GetFileName(file);
        try
        {
            MediaRecord record = ReadRecord(file);
            List<string> problems = Problems(user, name, record);
            if (problems.Count == 0)
            {
                return (ReadOutcome.Ok, new MediaEntry(record));
            }
            MediaLog.RecordRefused(_logger, name, string.Join("; ", problems));
            return (ReadOutcome.Invalid, null);
        }
        catch (ProjectFormatException exception)
        {
            MediaLog.RecordRefused(_logger, name, string.Join("; ", exception.Problems));
            return (ReadOutcome.Invalid, null);
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return (ReadOutcome.Gone, null);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            if (log)
            {
                MediaLog.RecordRefused(_logger, name, exception.GetType().Name);
            }
            return (ReadOutcome.Unreadable, null);
        }
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
                bool owned = record is not null
                    && (File.Exists(Path.Combine(media, record)) || File.Exists(Path.Combine(media, record + StorePaths.RefusedSuffix)));
                if (record is not null && !owned && TryDelete(file))
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
