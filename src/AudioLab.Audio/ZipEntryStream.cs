namespace AudioLab.Audio;

/// <summary>The write-only stream of one entry of a ZIP file: it passes the bytes on and counts them and their check value.</summary>
internal sealed class ZipEntryStream(Stream output) : Stream
{
    /// <summary>The check value of the bytes written.</summary>
    public uint Crc { get; private set; }

    public override bool CanRead => false;

    public override bool CanSeek => false;

    public override bool CanWrite => true;

    public override long Length => Written;

    public override long Position
    {
        get => Written;
        set => throw new NotSupportedException();
    }

    /// <summary>How many bytes have been written.</summary>
    public long Written { get; private set; }

    public override void Flush() => output.Flush();

    public override Task FlushAsync(CancellationToken cancellationToken) => output.FlushAsync(cancellationToken);

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        output.Write(buffer);
        Count(buffer);
    }

    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        await output.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
        Count(buffer.Span);
    }

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    private void Count(ReadOnlySpan<byte> buffer)
    {
        Crc = Crc32.Update(Crc, buffer);
        Written += buffer.Length;
    }
}
