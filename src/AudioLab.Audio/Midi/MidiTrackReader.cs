namespace AudioLab.Audio.Midi;

/// <summary>Reads the events of one track chunk in order and refuses anything a track may not hold.</summary>
internal ref struct MidiTrackReader
{
    // Controllers 120 to 127 are channel mode messages, such as all notes off, and are no part of a clip.
    private const int FirstChannelMode = 120;

    private readonly ReadOnlySpan<byte> _data;
    private readonly int _track;
    private int _position;
    private long _tick;
    private int _running;
    private bool _ended;

    /// <summary>Starts at the first event of the chunk's data, which is track number <paramref name="track"/>, counted from 1.</summary>
    public MidiTrackReader(ReadOnlySpan<byte> data, int track)
    {
        _data = data;
        _track = track;
    }

    /// <summary>Reads the next event; false once End of Track has been read, and an exception when the track is not whole.</summary>
    public bool TryNext(out MidiMessage message)
    {
        if (_ended)
        {
            message = default;
            return false;
        }
        if (_position >= _data.Length)
        {
            throw Refuse("the track ends without an End of Track event");
        }
        _tick += ReadVariableLength();
        int status = Peek();
        if (status >= 0x80)
        {
            _position++;
        }
        else if (_running == 0)
        {
            throw Refuse("a data byte comes where a status byte is needed and there is no running status");
        }
        else
        {
            status = _running;
        }
        message = status switch
        {
            0xFF => ReadMeta(),
            0xF0 or 0xF7 => ReadSystemExclusive(),
            >= 0xF0 => throw Refuse($"the system message {status:X2} cannot be in a file"),
            _ => ReadChannelMessage(status),
        };
        return true;
    }

    private MidiMessage ReadChannelMessage(int status)
    {
        _running = status;
        int channel = status & 0x0F;
        int command = status >> 4;
        int first = ReadData();
        int second = command is 0xC or 0xD ? 0 : ReadData();
        return command switch
        {
            0x8 => new MidiMessage(_tick, MidiMessageKind.NoteOff, channel, first, second),
            0x9 when second == 0 => new MidiMessage(_tick, MidiMessageKind.NoteOff, channel, first, 0),
            0x9 => new MidiMessage(_tick, MidiMessageKind.NoteOn, channel, first, second),
            0xB when first < FirstChannelMode => new MidiMessage(_tick, MidiMessageKind.Controller, channel, first, second),
            _ => new MidiMessage(_tick, MidiMessageKind.Ignored, channel, 0, 0),
        };
    }

    private MidiMessage ReadSystemExclusive()
    {
        _running = 0;
        long length = ReadVariableLength();
        Skip(length);
        return new MidiMessage(_tick, MidiMessageKind.Ignored, 0, 0, 0);
    }

    private MidiMessage ReadMeta()
    {
        _running = 0;
        int type = ReadByte();
        long length = ReadVariableLength();
        if (length > _data.Length - _position)
        {
            throw Refuse($"the meta event {type:X2} runs past the end of the track");
        }
        int size = (int)length;
        ReadOnlySpan<byte> body = _data.Slice(_position, size);
        MidiMessage message = type switch
        {
            0x2F => EndOfTrack(size),
            0x51 => Tempo(size, body),
            0x58 => TimeSignature(size, body),
            _ => Other(type, size),
        };
        _position += size;
        return message;
    }

    private MidiMessage EndOfTrack(int size)
    {
        RequireSize("End of Track", size, 0);
        if (_position != _data.Length)
        {
            throw Refuse("there are events after End of Track");
        }
        _ended = true;
        return new MidiMessage(_tick, MidiMessageKind.EndOfTrack, 0, 0, 0);
    }

    private MidiMessage Tempo(int size, ReadOnlySpan<byte> body)
    {
        RequireSize("tempo", size, 3);
        int microseconds = (body[0] << 16) | (body[1] << 8) | body[2];
        if (microseconds < MidiLimits.MinMicrosecondsPerQuarter)
        {
            throw Refuse($"a tempo of {microseconds} microseconds a quarter note is above 1000 beats a minute");
        }
        return new MidiMessage(_tick, MidiMessageKind.Tempo, 0, microseconds, 0);
    }

    private MidiMessage TimeSignature(int size, ReadOnlySpan<byte> body)
    {
        RequireSize("time signature", size, 4);
        return new MidiMessage(_tick, MidiMessageKind.TimeSignature, 0, body[0], body[1]);
    }

    // The lengths the Standard MIDI File specification fixes are checked for the meta events a reader would otherwise skip unseen.
    private MidiMessage Other(int type, int size)
    {
        int? fixedSize = type switch
        {
            0x20 or 0x21 => 1,
            0x54 => 5,
            0x59 => 2,
            _ => null,
        };
        if (fixedSize is { } expected)
        {
            RequireSize($"meta {type:X2}", size, expected);
        }
        else if (type == 0x00 && size is not (0 or 2))
        {
            throw Refuse($"a sequence number has {size} bytes, not 0 or 2");
        }
        return new MidiMessage(_tick, MidiMessageKind.Ignored, 0, 0, 0);
    }

    private readonly void RequireSize(string what, int size, int expected)
    {
        if (size != expected)
        {
            throw Refuse($"the {what} event has {size} bytes, not {expected}");
        }
    }

    private void Skip(long length)
    {
        if (length > _data.Length - _position)
        {
            throw Refuse("an event runs past the end of the track");
        }
        _position += (int)length;
    }

    private long ReadVariableLength()
    {
        long value = 0;
        for (int i = 0; i < 4; i++)
        {
            int next = ReadByte();
            value = (value << 7) | (long)(next & 0x7F);
            if (next < 0x80)
            {
                return value;
            }
        }
        throw Refuse("a variable-length number has more than four bytes");
    }

    private int ReadData()
    {
        int value = ReadByte();
        if (value >= 0x80)
        {
            throw Refuse("a data byte has its high bit set");
        }
        return value;
    }

    private int ReadByte()
    {
        if (_position >= _data.Length)
        {
            throw Refuse("an event runs past the end of the track");
        }
        return _data[_position++];
    }

    private readonly int Peek()
    {
        if (_position >= _data.Length)
        {
            throw Refuse("an event runs past the end of the track");
        }
        return _data[_position];
    }

    private readonly MidiFormatException Refuse(string reason) => new($"MIDI track {_track}, byte {_position} of its data: {reason}.");
}
