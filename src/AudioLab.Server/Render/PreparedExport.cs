using AudioLab.Audio;

namespace AudioLab.Server.Render;

/// <summary>An export that passed every check that can be made before its first byte, holding its place in the gate until it is disposed.</summary>
/// <remarks>
/// <see cref="Length"/> is the exact size of what <see cref="WriteAsync"/> sends, so a client can tell a finished file from a cut one.
/// Nothing is written to disk: the bytes go to the stream and a failure leaves nothing but what the stream already took.
/// </remarks>
public sealed class PreparedExport : IDisposable
{
    private readonly Func<Stream, CancellationToken, Task<WavResult>> _write;
    private readonly Hold _hold;
    private readonly ILogger _logger;

    internal PreparedExport(
        string contentType,
        string fileName,
        long length,
        Func<Stream, CancellationToken, Task<WavResult>> write,
        Hold hold,
        ILogger logger)
    {
        ContentType = contentType;
        FileName = fileName;
        Length = length;
        _write = write;
        _hold = hold;
        _logger = logger;
    }

    /// <summary>The content type of the file.</summary>
    public string ContentType { get; }

    /// <summary>A name for the file that holds no path or control character.</summary>
    public string FileName { get; }

    /// <summary>The exact length in bytes of the file that <see cref="WriteAsync"/> writes.</summary>
    public long Length { get; }

    /// <summary>Renders into <paramref name="output"/>, a block at a time, and stops when the client goes away or the time limit passes.</summary>
    /// <exception cref="ExportRefusedException">The time limit passed.</exception>
    /// <exception cref="OperationCanceledException">The client went away.</exception>
    public async Task WriteAsync(Stream output)
    {
        ArgumentNullException.ThrowIfNull(output);
        try
        {
            // On a worker: the renderer runs on the calling thread between the awaits, and the request's thread must not be held by it.
            WavResult result = await Task.Run(() => _write(output, _hold.Linked.Token), _hold.Linked.Token);
            if (result.HasProblems)
            {
                RenderLog.ExportClipped(_logger, result.ClippedSamples, result.NonFiniteSamples);
            }
        }
        catch (OperationCanceledException) when (_hold.Limit.IsCancellationRequested && !_hold.RequestAborted.IsCancellationRequested)
        {
            throw new ExportRefusedException(ExportRefusal.TimedOut, "The export took longer than the server allows; ask for a shorter range.");
        }
    }

    /// <summary>Gives up the export's place in the gate.</summary>
    public void Dispose() => _hold.Dispose();
}
