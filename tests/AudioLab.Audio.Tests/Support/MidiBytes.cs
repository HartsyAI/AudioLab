namespace AudioLab.Audio.Tests.Support;

/// <summary>Builds Standard MIDI File bytes by hand, so a test can make a file that a writer would never make.</summary>
public sealed class MidiBytes
{
    private readonly List<byte> _bytes = [];

    public MidiBytes Delta(long ticks)
    {
        Span<byte> parts = stackalloc byte[5];
        int count = 0;
        parts[count++] = (byte)(ticks & 0x7F);
        while ((ticks >>= 7) > 0)
        {
            parts[count++] = (byte)((ticks & 0x7F) | 0x80);
        }
        for (int i = count - 1; i >= 0; i--)
        {
            _bytes.Add(parts[i]);
        }
        return this;
    }

    public MidiBytes Raw(params byte[] bytes)
    {
        _bytes.AddRange(bytes);
        return this;
    }

    public MidiBytes NoteOn(long delta, int pitch, int velocity, int channel = 0) =>
        Delta(delta).Raw((byte)(0x90 | channel), (byte)pitch, (byte)velocity);

    public MidiBytes NoteOff(long delta, int pitch, int channel = 0) => Delta(delta).Raw((byte)(0x80 | channel), (byte)pitch, 0);

    public MidiBytes Controller(long delta, int controller, int value, int channel = 0) =>
        Delta(delta).Raw((byte)(0xB0 | channel), (byte)controller, (byte)value);

    public MidiBytes Tempo(long delta, int microseconds) =>
        Delta(delta).Raw(0xFF, 0x51, 0x03, (byte)(microseconds >> 16), (byte)(microseconds >> 8), (byte)microseconds);

    public MidiBytes TimeSignature(long delta, int numerator, int denominatorPower) =>
        Delta(delta).Raw(0xFF, 0x58, 0x04, (byte)numerator, (byte)denominatorPower, 24, 8);

    public MidiBytes End(long delta = 0) => Delta(delta).Raw(0xFF, 0x2F, 0x00);

    public byte[] Chunk(string type = "MTrk")
    {
        List<byte> chunk = [.. System.Text.Encoding.ASCII.GetBytes(type)];
        chunk.AddRange(BigEndian(_bytes.Count, 4));
        chunk.AddRange(_bytes);
        return [.. chunk];
    }

    public static byte[] File(int format, int division, params byte[][] chunks) => FileDeclaring(format, chunks.Length, division, chunks);

    public static byte[] FileDeclaring(int format, int tracks, int division, params byte[][] chunks)
    {
        List<byte> file = [.. "MThd"u8.ToArray()];
        file.AddRange(BigEndian(6, 4));
        file.AddRange(BigEndian(format, 2));
        file.AddRange(BigEndian(tracks, 2));
        file.AddRange(BigEndian(division, 2));
        foreach (byte[] chunk in chunks)
        {
            file.AddRange(chunk);
        }
        return [.. file];
    }

    public static byte[] Track(Action<MidiBytes> events)
    {
        MidiBytes track = new();
        events(track);
        return track.Chunk();
    }

    private static byte[] BigEndian(int value, int size)
    {
        byte[] bytes = new byte[size];
        for (int i = 0; i < size; i++)
        {
            bytes[size - 1 - i] = (byte)(value >> (8 * i));
        }
        return bytes;
    }
}
