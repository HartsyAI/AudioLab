using System.Security.Cryptography;
using AudioLab.Core.Model;
using AudioLab.Server.Store;

namespace AudioLab.Server.Media;

/// <summary>Everything a user can do with their media files. Every method takes the user from the door; none reads an owner from a request.</summary>
public sealed class MediaService(
    MediaStore store,
    MediaReferences references,
    StoreRoot root,
    IIdService ids,
    MediaOptions options,
    TimeProvider time,
    ILogger<MediaService> logger)
{
    private const int CopyBufferBytes = 80 * 1024;

    /// <summary>Stores an uploaded WAV file after checking every byte of its structure; nothing is kept for a file that fails.</summary>
    /// <param name="user">The user the door resolved, who becomes the owner.</param>
    /// <param name="name">The label the file is listed under.</param>
    /// <param name="body">The bytes of the file.</param>
    /// <param name="declaredLength">The length the client announced, or null for a chunked body; it is only used to refuse early.</param>
    /// <param name="cancellationToken">Cancels the upload; the partly written file is removed.</param>
    /// <exception cref="MediaRejectedException">The name or the file is not accepted.</exception>
    /// <exception cref="MediaTooLargeException">The body is larger than the upload limit, whatever the client announced.</exception>
    /// <exception cref="MediaQuotaExceededException">The user has no room for it, counting what their other uploads have received.</exception>
    /// <exception cref="MediaBusyException">The user already has as many uploads running as are allowed.</exception>
    public async Task<MediaRecord> UploadAsync(UserId user, string? name, Stream body, long? declaredLength, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        IReadOnlyList<string> nameProblems = MediaValidator.ValidateName(name);
        if (nameProblems.Count > 0)
        {
            throw new MediaRejectedException($"The name is not valid: {nameProblems[0]}");
        }
        if (declaredLength > options.MaxUploadBytes)
        {
            throw new MediaTooLargeException(options.MaxUploadBytes);
        }
        using MediaUpload upload = store.BeginUpload(user);
        await upload.RequireRoomAsync(declaredLength ?? 0, cancellationToken);

        MediaId id = ids.Next<MediaId>();
        string temp = root.Paths.MediaTempFile(user, id);
        Directory.CreateDirectory(Path.GetDirectoryName(temp)!);
        try
        {
            (long bytes, string hash) = await ReceiveAsync(body, temp, upload, cancellationToken);
            WavFormat format = await CheckAsync(temp, cancellationToken);
            MediaRecord record = new()
            {
                Id = id,
                Owner = user,
                Name = name!,
                Kind = MediaKind.Audio,
                Format = MediaFormat.Wav,
                Encoding = format.Encoding,
                Duration = format.Duration,
                SampleRate = format.SampleRate,
                Channels = format.Channels,
                Bytes = bytes,
                Sha256 = hash,
                Created = time.GetUtcNow(),
            };
            return await store.CommitAsync(record, temp, cancellationToken);
        }
        finally
        {
            // After a commit the file has been moved; this only removes what a failed, refused or cancelled upload left,
            // and must not hide the error that ended it.
            TryDelete(temp);
        }
    }

    /// <summary>A user's file.</summary>
    /// <exception cref="MediaNotFoundException">The user has no such file.</exception>
    public MediaRecord Get(UserId user, MediaId id) => store.Get(user, id);

    /// <summary>The user's files, newest first.</summary>
    public MediaListing List(UserId user)
    {
        (IReadOnlyList<MediaRecord> records, int refused) = store.List(user);
        return new MediaListing([.. records.Select(MediaSummary.Of)], refused);
    }

    /// <summary>Opens the bytes of a user's file for reading.</summary>
    /// <exception cref="MediaNotFoundException">The user has no such file.</exception>
    public FileStream OpenContent(UserId user, MediaId id) => store.OpenContent(user, id);

    /// <summary>Deletes a user's file unless a project of theirs still plays it, in the project's present or in its undo history.</summary>
    /// <remarks>
    /// The file is hidden from <see cref="MediaStore.Find"/> and from reads first, and only then are the projects searched.
    /// There is no way to force the delete.
    /// </remarks>
    /// <exception cref="MediaNotFoundException">The user has no such file.</exception>
    /// <exception cref="MediaDeleteInProgressException">Another delete of the file has begun and may still be refused.</exception>
    /// <exception cref="MediaInUseException">A project of the user plays it.</exception>
    public async Task DeleteAsync(UserId user, MediaId id, CancellationToken cancellationToken)
    {
        _ = store.BeginDelete(user, id);
        bool finished = false;
        try
        {
            if (await references.FindProjectAsync(user, id, cancellationToken) is { } project)
            {
                throw new MediaInUseException(project);
            }
            await store.FinishDeleteAsync(user, id, cancellationToken);
            finished = true;
        }
        finally
        {
            if (!finished)
            {
                store.CancelDelete(user, id);
            }
        }
    }

    // The limit is counted on the bytes that arrive, not on what the client announced.
    private async Task<(long Bytes, string Hash)> ReceiveAsync(Stream body, string path, MediaUpload upload, CancellationToken cancellationToken)
    {
        FileStreamOptions fileOptions = new()
        {
            Mode = FileMode.CreateNew,
            Access = FileAccess.Write,
            Share = FileShare.None,
            Options = FileOptions.Asynchronous,
        };
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        long total = 0;
        await using (FileStream file = new(path, fileOptions))
        {
            byte[] buffer = new byte[CopyBufferBytes];
            int read;
            while ((read = await body.ReadAsync(buffer, cancellationToken)) > 0)
            {
                total += read;
                if (total > options.MaxUploadBytes)
                {
                    throw new MediaTooLargeException(options.MaxUploadBytes);
                }
                await upload.AddAsync(read, cancellationToken);
                hash.AppendData(buffer, 0, read);
                await file.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            }
            file.Flush(flushToDisk: true);
        }
        return total == 0
            ? throw new MediaRejectedException("The upload is empty.")
            : (total, Convert.ToHexStringLower(hash.GetHashAndReset()));
    }

    private void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            MediaLog.NotRemoved(logger, Path.GetFileName(path), exception.GetType().Name);
        }
    }

    private static async Task<WavFormat> CheckAsync(string path, CancellationToken cancellationToken)
    {
        FileStreamOptions fileOptions = new()
        {
            Mode = FileMode.Open,
            Access = FileAccess.Read,
            Share = FileShare.Read,
            Options = FileOptions.Asynchronous,
        };
        await using FileStream file = new(path, fileOptions);
        WavFormat format = WavReader.Read(file);
        await WavReader.RequireFiniteAsync(file, format, cancellationToken);
        return format;
    }
}
