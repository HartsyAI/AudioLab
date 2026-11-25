using System.Text.Json;
using AudioLab.Core.Model;
using AudioLab.Core.Transcription;
using AudioLab.Server.Store;

namespace AudioLab.Server.Transcription;

/// <summary>The files of transcription jobs: state, chunk log and transcript, under each user's folder (D92, D94).</summary>
/// <remarks>
/// Every whole file is written through <see cref="AtomicFile"/>. The chunk log is appended one line a window; a line that does not end, or
/// does not parse, is cut off at the next read, so a restart in the middle of a write loses one chunk and never a line in the middle.
/// </remarks>
public sealed class TranscriptionStore(StoreRoot root, AtomicFile atomic, ILogger<TranscriptionStore> logger)
{
    private const byte NewLine = (byte)'\n';

    /// <summary>The store's paths.</summary>
    public StorePaths Paths => root.Paths;

    /// <summary>Writes a job's state file, atomically.</summary>
    public Task WriteStateAsync(UserId user, MediaId id, TranscriptionJobFile file, CancellationToken cancellationToken)
    {
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(file, TranscriptJsonContext.Default.TranscriptionJobFile);
        Directory.CreateDirectory(Paths.TranscriptsDirectory(user));
        return atomic.WriteAsync(Paths.TranscriptJobFile(user, id), bytes, cancellationToken);
    }

    /// <summary>Every job state file on disk, with the file's path; a file that cannot be read is logged and left out.</summary>
    public IEnumerable<(string Path, TranscriptionJobFile File)> ReadAllStates()
    {
        if (!Directory.Exists(Paths.UsersDirectory))
        {
            yield break;
        }
        foreach (string folder in Directory.EnumerateDirectories(Paths.UsersDirectory))
        {
            string transcripts = Path.Combine(folder, "transcripts");
            if (!Directory.Exists(transcripts))
            {
                continue;
            }
            foreach (string path in Directory.EnumerateFiles(transcripts, "t*.job.json"))
            {
                TranscriptionJobFile? file = ReadState(path);
                if (file is not null)
                {
                    yield return (path, file);
                }
            }
        }
    }

    /// <summary>Appends one window's segments to the chunk log; the chunk is kept once the write is flushed.</summary>
    public async Task AppendChunkAsync(UserId user, MediaId id, ChunkRecord chunk, CancellationToken cancellationToken)
    {
        byte[] line = JsonSerializer.SerializeToUtf8Bytes(chunk, TranscriptJsonContext.Default.ChunkRecord);
        byte[] bytes = new byte[line.Length + 1];
        line.CopyTo(bytes, 0);
        bytes[^1] = NewLine;
        Directory.CreateDirectory(Paths.TranscriptsDirectory(user));
        await using FileStream stream = new(
            Paths.TranscriptChunkFile(user, id),
            FileMode.Append,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 1,
            FileOptions.Asynchronous | FileOptions.WriteThrough);
        await stream.WriteAsync(bytes, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    /// <summary>The chunks the log holds in order, from the first; a torn or damaged tail is cut off and the file is rewritten without it.</summary>
    public async Task<IReadOnlyList<ChunkRecord>> ReadChunksAsync(UserId user, MediaId id, CancellationToken cancellationToken)
    {
        string path = Paths.TranscriptChunkFile(user, id);
        if (!File.Exists(path))
        {
            return [];
        }
        byte[] bytes = await File.ReadAllBytesAsync(path, cancellationToken);
        List<ChunkRecord> kept = [];
        int position = 0;
        while (position < bytes.Length)
        {
            int end = Array.IndexOf(bytes, NewLine, position);
            if (end < 0 || !TryReadChunk(bytes.AsSpan(position, end - position), kept.Count, out ChunkRecord? chunk))
            {
                break;
            }
            kept.Add(chunk!);
            position = end + 1;
        }
        if (position < bytes.Length)
        {
            TranscriptionLog.DamagedTail(logger, bytes.Length - position);
            await atomic.WriteAsync(path, bytes.AsMemory(0, position), cancellationToken);
        }
        return kept;
    }

    /// <summary>Removes a job's chunk log, so its work starts again.</summary>
    public void DeleteChunks(UserId user, MediaId id) => DeleteFile(Paths.TranscriptChunkFile(user, id));

    /// <summary>Writes a transcript, atomically, after it has been checked; a document the server would not accept is refused.</summary>
    /// <exception cref="TranscriptRefusedException">The document is not one the server stores.</exception>
    public Task WriteTranscriptAsync(UserId user, MediaId id, TranscriptDocument document, CancellationToken cancellationToken)
    {
        IReadOnlyList<string> problems = TranscriptRules.DocumentProblems(document);
        if (problems.Count > 0)
        {
            throw new TranscriptRefusedException(problems);
        }
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(document, TranscriptJsonContext.Default.TranscriptDocument);
        if (bytes.Length > TranscriptionLimits.MaxTranscriptBytes)
        {
            throw new TranscriptRefusedException(["The transcript is larger than the server stores."]);
        }
        Directory.CreateDirectory(Paths.TranscriptsDirectory(user));
        return atomic.WriteAsync(Paths.TranscriptFile(user, id), bytes, cancellationToken);
    }

    /// <summary>Reads a stored transcript, or null when there is none; a file that is not sound is refused, never repaired.</summary>
    /// <exception cref="TranscriptRefusedException">The file is larger than the limit, is not JSON of the format, or fails its checks.</exception>
    public async Task<TranscriptDocument?> ReadTranscriptAsync(UserId user, MediaId id, CancellationToken cancellationToken)
    {
        string path = Paths.TranscriptFile(user, id);
        if (!File.Exists(path))
        {
            return null;
        }
        if (new FileInfo(path).Length > TranscriptionLimits.MaxTranscriptBytes)
        {
            throw new TranscriptRefusedException(["The stored transcript is larger than the server accepts."]);
        }
        byte[] bytes = await File.ReadAllBytesAsync(path, cancellationToken);
        TranscriptDocument? document;
        try
        {
            document = JsonSerializer.Deserialize(bytes, TranscriptJsonContext.Default.TranscriptDocument);
        }
        catch (JsonException exception)
        {
            throw new TranscriptRefusedException([$"The stored transcript is not valid JSON of this format: {exception.Message}"]);
        }
        if (document is null)
        {
            throw new TranscriptRefusedException(["The stored transcript is empty."]);
        }
        IReadOnlyList<string> problems = TranscriptRules.DocumentProblems(document);
        if (problems.Count > 0)
        {
            throw new TranscriptRefusedException(problems);
        }
        if (document.Media != id.Value)
        {
            throw new TranscriptRefusedException(["The stored transcript names another media file."]);
        }
        return document;
    }

    /// <summary>Removes every file of a job and its transcript; a file that is already gone is not an error.</summary>
    public void DeleteAll(UserId user, MediaId id)
    {
        DeleteFile(Paths.TranscriptJobFile(user, id));
        DeleteFile(Paths.TranscriptChunkFile(user, id));
        DeleteFile(Paths.TranscriptFile(user, id));
    }

    /// <summary>Removes temporary files that an interrupted write left in the transcription folders.</summary>
    public void SweepTemporaryFiles()
    {
        if (!Directory.Exists(Paths.UsersDirectory))
        {
            return;
        }
        foreach (string folder in Directory.EnumerateDirectories(Paths.UsersDirectory))
        {
            string transcripts = Path.Combine(folder, "transcripts");
            if (!Directory.Exists(transcripts))
            {
                continue;
            }
            foreach (string temp in Directory.EnumerateFiles(transcripts, "t*.tmp"))
            {
                DeleteFile(temp);
            }
        }
    }

    private TranscriptionJobFile? ReadState(string path)
    {
        try
        {
            return JsonSerializer.Deserialize(File.ReadAllBytes(path), TranscriptJsonContext.Default.TranscriptionJobFile);
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            TranscriptionLog.FileFailed(logger, exception.GetType().Name);
            return null;
        }
    }

    private static bool TryReadChunk(ReadOnlySpan<byte> line, int expected, out ChunkRecord? chunk)
    {
        try
        {
            chunk = JsonSerializer.Deserialize(line, TranscriptJsonContext.Default.ChunkRecord);
        }
        catch (JsonException)
        {
            chunk = null;
            return false;
        }
        return chunk is not null && chunk.Chunk == expected;
    }

    private void DeleteFile(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            TranscriptionLog.FileFailed(logger, exception.GetType().Name);
        }
    }
}
