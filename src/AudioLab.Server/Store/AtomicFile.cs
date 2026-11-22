using Microsoft.Extensions.Logging.Abstractions;

namespace AudioLab.Server.Store;

/// <summary>Writes a file so that a reader, or a crash, sees the whole old content or the whole new content and nothing between.</summary>
/// <remarks>The content goes to a temporary file beside the destination, is flushed to disk, and is renamed over the destination.</remarks>
public sealed class AtomicFile
{
    private const int RenameAttempts = 10;

    private readonly Action<AtomicWriteStage, string>? _observer;
    private readonly ILogger _logger;

    /// <summary>Creates a writer.</summary>
    /// <param name="logger">Receives a warning when a temporary file cannot be removed.</param>
    /// <param name="observer">Called at each <see cref="AtomicWriteStage"/> with the temporary file's path; a test uses it to look at the disk or to fail the write.</param>
    public AtomicFile(ILogger<AtomicFile>? logger = null, Action<AtomicWriteStage, string>? observer = null)
    {
        _logger = logger ?? NullLogger<AtomicFile>.Instance;
        _observer = observer;
    }

    /// <summary>Replaces the file at <paramref name="path"/> with <paramref name="content"/>, or leaves it as it was.</summary>
    public async Task WriteAsync(string path, ReadOnlyMemory<byte> content, CancellationToken cancellationToken)
    {
        string directory = Path.GetDirectoryName(path) ?? throw new ArgumentException("The path has no folder.", nameof(path));
        Directory.CreateDirectory(directory);
        string temp = $"{path}.{Guid.NewGuid():N}.tmp";
        bool replaced = false;
        try
        {
            FileStreamOptions options = new()
            {
                Mode = FileMode.CreateNew,
                Access = FileAccess.Write,
                Share = FileShare.None,
                Options = FileOptions.Asynchronous | FileOptions.WriteThrough,
                BufferSize = 0,
            };
            await using (FileStream stream = new(temp, options))
            {
                _observer?.Invoke(AtomicWriteStage.TempCreated, temp);
                int half = content.Length / 2;
                await stream.WriteAsync(content[..half], cancellationToken);
                _observer?.Invoke(AtomicWriteStage.PartlyWritten, temp);
                await stream.WriteAsync(content[half..], cancellationToken);
                stream.Flush(flushToDisk: true);
                _observer?.Invoke(AtomicWriteStage.Flushed, temp);
            }
            _observer?.Invoke(AtomicWriteStage.BeforeRename, temp);
            await ReplaceAsync(temp, path, cancellationToken);
            replaced = true;
            _observer?.Invoke(AtomicWriteStage.Renamed, temp);
        }
        finally
        {
            if (!replaced)
            {
                Remove(temp);
            }
        }
    }

    // Another process that holds the destination open, such as an indexer or a backup, can make a rename fail for a moment on Windows.
    private static async Task ReplaceAsync(string temp, string path, CancellationToken cancellationToken)
    {
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                File.Move(temp, path, overwrite: true);
                return;
            }
            catch (Exception exception) when (attempt < RenameAttempts && exception is IOException or UnauthorizedAccessException)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(5 * attempt), cancellationToken);
            }
        }
    }

    private void Remove(string temp)
    {
        try
        {
            File.Delete(temp);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            StoreLog.TempNotRemoved(_logger, exception);
        }
    }
}
