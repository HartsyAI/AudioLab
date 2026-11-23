namespace AudioLab.Web.Services;

/// <summary>A read-only view of a stream that reports how far it has been read, a step at a time, and disposes the stream with itself.</summary>
internal sealed class ProgressStream(Stream inner, long total, IProgress<UploadProgress>? progress) : Stream
{
    private const long MinStep = 64 * 1024;

    private readonly long _step = Math.Max(MinStep, total / 200);

    private long _read;

    private long _reported;

    /// <inheritdoc />
    public override bool CanRead => true;

    /// <inheritdoc />
    public override bool CanSeek => false;

    /// <inheritdoc />
    public override bool CanWrite => false;

    /// <inheritdoc />
    public override long Length => throw new NotSupportedException();

    /// <inheritdoc />
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    /// <inheritdoc />
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        int count = await inner.ReadAsync(buffer, cancellationToken);
        Count(count);
        return count;
    }

    /// <inheritdoc />
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        return ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
    }

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

    /// <inheritdoc />
    public override int Read(Span<byte> buffer)
    {
        int count = inner.Read(buffer);
        Count(count);
        return count;
    }

    /// <inheritdoc />
    public override void Flush()
    {
    }

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            inner.Dispose();
        }
        base.Dispose(disposing);
    }

    private void Count(int count)
    {
        _read += count;
        if (progress is not null && (count == 0 || _read - _reported >= _step))
        {
            _reported = _read;
            progress.Report(new UploadProgress(_read, total));
        }
    }
}
