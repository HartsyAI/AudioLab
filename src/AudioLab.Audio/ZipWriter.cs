using System.Buffers.Binary;
using System.Text;

namespace AudioLab.Audio;

/// <summary>Writes a ZIP file of stored entries to a stream that cannot seek, an entry at a time, with a data descriptor after each.</summary>
/// <remarks>
/// The central directory that makes the file readable comes last and only from <see cref="Finish"/>, so an export that fails part way
/// leaves a file that no reader will open as complete. There is no ZIP64: the file is at most 4 GiB and holds at most 65,535 entries.
/// </remarks>
internal sealed class ZipWriter(Stream output)
{
    /// <summary>The most entries a plain ZIP file holds.</summary>
    public const int MaxEntries = ushort.MaxValue;

    // 1980-01-01 00:00, so that the same entries always give the same bytes.
    private const ushort DosTime = 0;
    private const ushort DosDate = 0x21;
    private const ushort Flags = 0x0808;
    private const ushort Version = 20;

    private readonly List<Entry> _entries = [];
    private long _offset;
    private ZipEntryStream? _open;
    private Entry? _current;

    /// <summary>The bytes of the local header of an entry and its data descriptor, which come with the entry's data.</summary>
    public static long EntryOverhead(string name) => 30 + Encoding.UTF8.GetByteCount(name) + 16;

    /// <summary>The bytes the central directory takes for an entry.</summary>
    public static long DirectoryOverhead(string name) => 46 + Encoding.UTF8.GetByteCount(name);

    /// <summary>The bytes of the end record.</summary>
    public static long EndRecordSize => 22;

    /// <summary>Starts an entry and returns the stream its data goes to.</summary>
    public Stream StartEntry(string name)
    {
        (byte[] header, Entry entry) = Begin(name);
        output.Write(header, 0, header.Length);
        return Open(entry);
    }

    /// <summary>Starts an entry on a stream that takes asynchronous writes.</summary>
    public async ValueTask<Stream> StartEntryAsync(string name, CancellationToken cancellationToken = default)
    {
        (byte[] header, Entry entry) = Begin(name);
        await output.WriteAsync(header, cancellationToken).ConfigureAwait(false);
        return Open(entry);
    }

    /// <summary>Ends the entry that was started, with its data descriptor.</summary>
    public void FinishEntry()
    {
        byte[] descriptor = Close();
        output.Write(descriptor, 0, descriptor.Length);
    }

    /// <summary>Ends the entry that was started, on a stream that takes asynchronous writes.</summary>
    public async ValueTask FinishEntryAsync(CancellationToken cancellationToken = default)
    {
        byte[] descriptor = Close();
        await output.WriteAsync(descriptor, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Writes the central directory and the end record, which makes the file complete.</summary>
    public void Finish()
    {
        byte[] directory = BuildDirectory();
        output.Write(directory, 0, directory.Length);
        output.Flush();
    }

    /// <summary>Writes the central directory and the end record to a stream that takes asynchronous writes.</summary>
    public async ValueTask FinishAsync(CancellationToken cancellationToken = default)
    {
        byte[] directory = BuildDirectory();
        await output.WriteAsync(directory, cancellationToken).ConfigureAwait(false);
        await output.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private (byte[] Header, Entry Entry) Begin(string name)
    {
        if (_open is not null)
        {
            throw new InvalidOperationException("The previous entry was not finished.");
        }
        if (_entries.Count >= MaxEntries)
        {
            throw new InvalidOperationException("A ZIP file without ZIP64 holds at most 65,535 entries.");
        }
        byte[] nameBytes = Encoding.UTF8.GetBytes(name);
        byte[] header = new byte[30 + nameBytes.Length];
        BinaryPrimitives.WriteUInt32LittleEndian(header, 0x04034B50);
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(4), Version);
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(6), Flags);
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(10), DosTime);
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(12), DosDate);
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(26), (ushort)nameBytes.Length);
        nameBytes.CopyTo(header, 30);
        return (header, new Entry(nameBytes, _offset));
    }

    private ZipEntryStream Open(Entry entry)
    {
        _offset += 30 + entry.Name.Length;
        _current = entry;
        _open = new ZipEntryStream(output);
        return _open;
    }

    private byte[] Close()
    {
        if (_open is null || _current is null)
        {
            throw new InvalidOperationException("No entry was started.");
        }
        if (_open.Written > uint.MaxValue)
        {
            throw new InvalidOperationException("An entry of a ZIP file without ZIP64 is at most 4 GiB.");
        }
        Entry done = _current with { Crc = _open.Crc, Size = (uint)_open.Written };
        _entries.Add(done);
        byte[] descriptor = new byte[16];
        BinaryPrimitives.WriteUInt32LittleEndian(descriptor, 0x08074B50);
        BinaryPrimitives.WriteUInt32LittleEndian(descriptor.AsSpan(4), done.Crc);
        BinaryPrimitives.WriteUInt32LittleEndian(descriptor.AsSpan(8), done.Size);
        BinaryPrimitives.WriteUInt32LittleEndian(descriptor.AsSpan(12), done.Size);
        _offset += done.Size + descriptor.Length;
        _open = null;
        _current = null;
        return descriptor;
    }

    private byte[] BuildDirectory()
    {
        if (_open is not null)
        {
            throw new InvalidOperationException("The last entry was not finished.");
        }
        long start = _offset;
        using MemoryStream directory = new();
        foreach (Entry entry in _entries)
        {
            byte[] record = new byte[46 + entry.Name.Length];
            BinaryPrimitives.WriteUInt32LittleEndian(record, 0x02014B50);
            BinaryPrimitives.WriteUInt16LittleEndian(record.AsSpan(4), Version);
            BinaryPrimitives.WriteUInt16LittleEndian(record.AsSpan(6), Version);
            BinaryPrimitives.WriteUInt16LittleEndian(record.AsSpan(8), Flags);
            BinaryPrimitives.WriteUInt16LittleEndian(record.AsSpan(12), DosTime);
            BinaryPrimitives.WriteUInt16LittleEndian(record.AsSpan(14), DosDate);
            BinaryPrimitives.WriteUInt32LittleEndian(record.AsSpan(16), entry.Crc);
            BinaryPrimitives.WriteUInt32LittleEndian(record.AsSpan(20), entry.Size);
            BinaryPrimitives.WriteUInt32LittleEndian(record.AsSpan(24), entry.Size);
            BinaryPrimitives.WriteUInt16LittleEndian(record.AsSpan(28), (ushort)entry.Name.Length);
            BinaryPrimitives.WriteUInt32LittleEndian(record.AsSpan(42), (uint)entry.Offset);
            entry.Name.CopyTo(record, 46);
            directory.Write(record);
        }
        byte[] end = new byte[22];
        BinaryPrimitives.WriteUInt32LittleEndian(end, 0x06054B50);
        BinaryPrimitives.WriteUInt16LittleEndian(end.AsSpan(8), (ushort)_entries.Count);
        BinaryPrimitives.WriteUInt16LittleEndian(end.AsSpan(10), (ushort)_entries.Count);
        BinaryPrimitives.WriteUInt32LittleEndian(end.AsSpan(12), (uint)directory.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(end.AsSpan(16), (uint)start);
        directory.Write(end);
        return directory.ToArray();
    }

    private sealed record Entry(byte[] Name, long Offset)
    {
        public uint Crc { get; init; }

        public uint Size { get; init; }
    }
}
