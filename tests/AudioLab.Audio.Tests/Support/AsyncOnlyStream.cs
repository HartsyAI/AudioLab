namespace AudioLab.Audio.Tests.Support;

/// <summary>A stream that takes asynchronous writes only, as a response body of a server does, and keeps what it was given.</summary>
public sealed class AsyncOnlyStream : Stream
{
    private readonly MemoryStream _inner = new();

    /// <summary>Called after every write with the total number of bytes so far.</summary>
    public Action<long>? OnWrite { get; init; }

    public byte[] Bytes => _inner.ToArray();

    public override bool CanRead => false;

    public override bool CanSeek => false;

    public override bool CanWrite => true;

    public override long Length => _inner.Length;

    public override long Position
    {
        get => _inner.Position;
        set => throw new NotSupportedException();
    }

    public override void Flush() => throw new NotSupportedException("synchronous flush");

    public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException("synchronous write");

    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        await _inner.WriteAsync(buffer, cancellationToken);
        OnWrite?.Invoke(_inner.Length);
    }

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
}
