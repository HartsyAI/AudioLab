using System.Collections.Concurrent;
using System.Text;
using AudioLab.Core.Model;
using AudioLab.Server.Store;

namespace AudioLab.Server.Media;

/// <summary>The media files of every user on disk, with an index in memory that the command pipeline can ask without waiting.</summary>
/// <remarks>
/// A file is a record (<c>m&lt;hash&gt;.json</c>) and its bytes (<c>m&lt;hash&gt;.wav</c>) in the owner's media folder, under the same
/// hashed names and path checks as projects. The bytes are renamed into place first and the record is written last, so a
/// record always has its bytes; bytes with no record are removed at start. A user's folder is read once, when it is first
/// asked about, and after that the index is the truth: one process at a time holds the store.
/// </remarks>
public sealed class MediaStore : IMediaCatalog
{
    private const int MaxRecordBytes = 8192;

    private static readonly UTF8Encoding _strictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private readonly StoreRoot _root;
    private readonly IMediaQuota _quota;
    private readonly ILogger<MediaStore> _logger;
    private readonly ConcurrentDictionary<UserId, UserMedia> _users = new();

    /// <summary>Opens the media of the store and removes bytes that have no record, which only a crash leaves.</summary>
    public MediaStore(StoreRoot root, IMediaQuota quota, ILogger<MediaStore> logger)
    {
        _root = root;
        _quota = quota;
        _logger = logger;
        MediaLog.OrphansSwept(logger, SweepOrphans());
    }

    /// <inheritdoc />
    public MediaRecord? Find(UserId owner, MediaId id)
    {
        UserMedia media = Loaded(owner);
        lock (media.Sync)
        {
            return media.Entries.TryGetValue(id, out Entry? entry) && !entry.PendingDelete ? entry.Record : null;
        }
    }

    /// <summary>A user's file.</summary>
    /// <exception cref="MediaNotFoundException">The user has no such file.</exception>
    public MediaRecord Get(UserId user, MediaId id) => Find(user, id) ?? throw new MediaNotFoundException();

    /// <summary>A user's files, newest first, and how many stored records were refused as invalid.</summary>
    public (IReadOnlyList<MediaRecord> Records, int Refused) List(UserId user)
    {
        UserMedia media = Loaded(user);
        lock (media.Sync)
        {
            List<MediaRecord> records =
            [
                .. media.Entries.Values
                    .Where(entry => !entry.PendingDelete)
                    .Select(entry => entry.Record)
                    .OrderByDescending(record => record.Created)
                    .ThenBy(record => record.Id.Value, StringComparer.Ordinal),
            ];
            return (records, media.Refused);
        }
    }

    /// <summary>What a user has stored, counting a file that is being deleted until it is gone.</summary>
    public MediaUsage UsageOf(UserId user)
    {
        UserMedia media = Loaded(user);
        lock (media.Sync)
        {
            return new MediaUsage(media.Entries.Count, media.Entries.Values.Sum(entry => entry.Record.Bytes));
        }
    }

    /// <summary>Refuses early when the user could not store <paramref name="incomingBytes"/> more; the commit checks again under its lock.</summary>
    /// <exception cref="MediaQuotaExceededException">The quota says no.</exception>
    public void RequireRoom(UserId user, long incomingBytes)
    {
        if (_quota.Refusal(user, UsageOf(user), incomingBytes) is { } refusal)
        {
            throw new MediaQuotaExceededException(refusal);
        }
    }

    /// <summary>Stores a file: renames its bytes and peaks into place, then writes its record, under the user's lock (the quota).</summary>
    /// <param name="record">The record of the file, with the id the temporary files' names were made from.</param>
    /// <param name="tempData">The finished temporary file of the bytes; it is moved, so it must be on the same volume.</param>
    /// <param name="tempPeaks">The finished temporary file of the peaks, moved in the same way.</param>
    /// <param name="cancellationToken">Cancels the commit before the record is written.</param>
    /// <exception cref="MediaQuotaExceededException">The quota says no.</exception>
    public async Task<MediaRecord> CommitAsync(MediaRecord record, string tempData, string tempPeaks, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);
        UserMedia media = Loaded(record.Owner);
        byte[] json = Encoding.UTF8.GetBytes(MediaJson.Write(record));
        await media.Gate.WaitAsync(cancellationToken);
        try
        {
            if (_quota.Refusal(record.Owner, UsageOf(record.Owner), record.Bytes) is { } refusal)
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
                TryDelete(data);
                TryDelete(peaks);
                throw;
            }
            lock (media.Sync)
            {
                media.Entries[record.Id] = new Entry(record);
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

    /// <summary>Opens the peaks of a user's file for reading.</summary>
    /// <exception cref="MediaNotFoundException">The user has no such file.</exception>
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

    /// <summary>Hides a file from every lookup so no command can name it any more; the first step of a delete.</summary>
    /// <returns>The record, or null when the user has no such file or its delete has already begun.</returns>
    public MediaRecord? BeginDelete(UserId user, MediaId id)
    {
        UserMedia media = Loaded(user);
        lock (media.Sync)
        {
            if (!media.Entries.TryGetValue(id, out Entry? entry) || entry.PendingDelete)
            {
                return null;
            }
            entry.PendingDelete = true;
            return entry.Record;
        }
    }

    /// <summary>Makes a file visible again after <see cref="BeginDelete"/>, because the delete was refused.</summary>
    public void CancelDelete(UserId user, MediaId id)
    {
        UserMedia media = Loaded(user);
        lock (media.Sync)
        {
            if (media.Entries.TryGetValue(id, out Entry? entry))
            {
                entry.PendingDelete = false;
            }
        }
    }

    /// <summary>Removes a file whose delete was begun: its record first, so a crash leaves bytes to sweep and never a record without bytes.</summary>
    public async Task FinishDeleteAsync(UserId user, MediaId id, CancellationToken cancellationToken)
    {
        UserMedia media = Loaded(user);
        await media.Gate.WaitAsync(cancellationToken);
        try
        {
            File.Delete(_root.Paths.MediaRecordFile(user, id));
            lock (media.Sync)
            {
                media.Entries.Remove(id);
            }
            TryDelete(_root.Paths.MediaDataFile(user, id));
            TryDelete(_root.Paths.MediaPeaksFile(user, id));
        }
        finally
        {
            media.Gate.Release();
        }
    }

    private static FileStream OpenForReading(string path)
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

    private UserMedia Loaded(UserId user)
    {
        UserMedia media = _users.GetOrAdd(user, _ => new UserMedia());
        lock (media.Sync)
        {
            if (!media.Loaded)
            {
                media.Loaded = Load(user, media);
            }
        }
        return media;
    }

    // False when the folder cannot be listed: the index stays empty, so nothing is found, and the next call tries again.
    private bool Load(UserId user, UserMedia media)
    {
        string directory = _root.Paths.MediaDirectory(user);
        try
        {
            if (!Directory.Exists(directory))
            {
                return true;
            }
            foreach (string file in Directory.EnumerateFiles(directory).Where(file => StorePaths.IsRecordFileName(Path.GetFileName(file))))
            {
                LoadRecord(user, media, file);
            }
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            media.Entries.Clear();
            media.Refused = 0;
            MediaLog.FolderUnreadable(_logger, exception.GetType().Name);
            return false;
        }
    }

    private void LoadRecord(UserId user, UserMedia media, string file)
    {
        string name = Path.GetFileName(file);
        try
        {
            MediaRecord record = ReadRecord(file);
            List<string> problems = Problems(user, name, record);
            if (problems.Count == 0)
            {
                media.Entries[record.Id] = new Entry(record);
                return;
            }
            media.Refused++;
            MediaLog.RecordRefused(_logger, name, string.Join("; ", problems));
        }
        catch (ProjectFormatException exception)
        {
            media.Refused++;
            MediaLog.RecordRefused(_logger, name, string.Join("; ", exception.Problems));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // One file another process holds must not hide the rest of the user's media.
            media.Refused++;
            MediaLog.RecordRefused(_logger, name, exception.GetType().Name);
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
            else if (PeaksProblem(user, record) is { } peaks)
            {
                problems.Add($"peaks: {peaks}");
            }
        }
        return problems;
    }

    // The peak file must be whole and describe this very file; the values themselves are only read when served.
    private string? PeaksProblem(UserId user, MediaRecord record)
    {
        string path = _root.Paths.MediaPeaksFile(user, record.Id);
        try
        {
            using FileStream stream = OpenForReading(path);
            byte[] head = new byte[Math.Min(stream.Length, PeakFile.HeaderBytes + (PeakFile.LevelEntryBytes * PeakFile.MaxLevels))];
            stream.ReadExactly(head);
            PeakHeader header = PeakFile.ReadHeader(head);
            long frames = (long)Math.Round(record.Duration * record.SampleRate);
            bool same = header.Channels == record.Channels && header.SampleRate == record.SampleRate && header.Frames == frames;
            return !same ? "the peak file describes another file."
                : header.TotalBytes != stream.Length ? "the peak file is not the size its header says."
                : null;
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return "the peak file is missing.";
        }
        catch (InvalidDataException exception)
        {
            return exception.Message;
        }
    }

    private int SweepOrphans()
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

    private sealed class Entry(MediaRecord record)
    {
        public MediaRecord Record { get; } = record;

        public bool PendingDelete { get; set; }
    }

    private sealed class UserMedia
    {
        public object Sync { get; } = new();

        public SemaphoreSlim Gate { get; } = new(1, 1);

        public Dictionary<MediaId, Entry> Entries { get; } = [];

        public bool Loaded { get; set; }

        public int Refused { get; set; }
    }
}
