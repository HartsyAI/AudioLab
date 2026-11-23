using System.Buffers.Binary;
using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Audio.Midi;

/// <summary>Reads a Standard MIDI File of format 0 or 1 into notes, controllers and a tempo map, and refuses anything malformed.</summary>
internal static class MidiReader
{
    private const int HeaderSize = 14;
    private const int ChunkHeaderSize = 8;
    private const int LargestDenominatorPower = 5;
    private const int MaxNumerator = 64;
    private const double MinChangeTime = 0.01;
    private const double MinChangeSpacing = 0.001;

    /// <summary>Reads the file, or throws <see cref="MidiFormatException"/> with the reason it cannot be read.</summary>
    public static MidiImport Read(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length > MidiLimits.MaxFileBytes)
        {
            throw new MidiFormatException($"The MIDI file is {bytes.Length} bytes, over the limit of {MidiLimits.MaxFileBytes}.");
        }
        (int format, int ticksPerQuarter, int trackCount, int firstChunk) = ReadHeader(bytes);
        List<(int Offset, int Length)> chunks = FindTracks(bytes, firstChunk);
        if (chunks.Count != trackCount)
        {
            throw new MidiFormatException($"The header declares {trackCount} tracks and the file holds {chunks.Count}.");
        }
        if (format == 0 && trackCount != 1)
        {
            throw new MidiFormatException($"A format 0 file has one track, not {trackCount}.");
        }
        MidiCollector collector = new();
        for (int i = 0; i < chunks.Count; i++)
        {
            ReadTrack(bytes.Slice(chunks[i].Offset, chunks[i].Length), i + 1, collector);
        }
        return Build(collector, ticksPerQuarter);
    }

    private static (int Format, int TicksPerQuarter, int Tracks, int FirstChunk) ReadHeader(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < HeaderSize || !bytes[..4].SequenceEqual("MThd"u8))
        {
            throw new MidiFormatException("This is not a Standard MIDI File: it does not start with an MThd header.");
        }
        uint length = BinaryPrimitives.ReadUInt32BigEndian(bytes[4..]);
        if (length < 6 || length > bytes.Length - ChunkHeaderSize)
        {
            throw new MidiFormatException($"The MIDI header declares {length} bytes, which is under 6 or past the end of the file.");
        }
        int format = BinaryPrimitives.ReadUInt16BigEndian(bytes[8..]);
        int tracks = BinaryPrimitives.ReadUInt16BigEndian(bytes[10..]);
        int division = BinaryPrimitives.ReadUInt16BigEndian(bytes[12..]);
        if (format > 1)
        {
            throw new MidiFormatException($"MIDI format {format} is not supported; only format 0 and 1 are.");
        }
        if ((division & 0x8000) != 0)
        {
            throw new MidiFormatException("A MIDI file timed in SMPTE frames is not supported; only ticks a quarter note are.");
        }
        if (division == 0)
        {
            throw new MidiFormatException("The MIDI file has 0 ticks a quarter note.");
        }
        if (tracks is < 1 or > MidiLimits.MaxTracks)
        {
            throw new MidiFormatException($"The MIDI file declares {tracks} tracks; 1 to {MidiLimits.MaxTracks} are accepted.");
        }
        return (format, division, tracks, ChunkHeaderSize + (int)length);
    }

    private static List<(int Offset, int Length)> FindTracks(ReadOnlySpan<byte> bytes, int first)
    {
        List<(int Offset, int Length)> tracks = [];
        int position = first;
        while (position < bytes.Length)
        {
            if (bytes.Length - position < ChunkHeaderSize)
            {
                throw new MidiFormatException($"The file ends inside a chunk header at byte {position}, or has bytes after its last chunk.");
            }
            uint length = BinaryPrimitives.ReadUInt32BigEndian(bytes[(position + 4)..]);
            if (length > bytes.Length - position - ChunkHeaderSize)
            {
                throw new MidiFormatException($"The chunk at byte {position} declares {length} bytes, past the end of the file.");
            }
            if (bytes.Slice(position, 4).SequenceEqual("MTrk"u8))
            {
                if (tracks.Count >= MidiLimits.MaxTracks)
                {
                    throw new MidiFormatException($"The file holds more than {MidiLimits.MaxTracks} tracks.");
                }
                tracks.Add((position + ChunkHeaderSize, (int)length));
            }
            position += ChunkHeaderSize + (int)length;
        }
        return tracks;
    }

    private static void ReadTrack(ReadOnlySpan<byte> data, int track, MidiCollector collector)
    {
        MidiTrackReader reader = new(data, track);
        Dictionary<int, Queue<(long Tick, int Velocity, int Order)>> open = [];
        long end = 0;
        while (reader.TryNext(out MidiMessage message))
        {
            end = message.Tick;
            switch (message.Kind)
            {
                case MidiMessageKind.NoteOn:
                    collector.NoteOn(open, message);
                    break;
                case MidiMessageKind.NoteOff:
                    collector.NoteOff(open, message);
                    break;
                case MidiMessageKind.Controller:
                    collector.Controller(message);
                    break;
                case MidiMessageKind.Tempo:
                    collector.Tempo(message);
                    break;
                case MidiMessageKind.TimeSignature:
                    collector.TimeSignature(message);
                    break;
                default:
                    break;
            }
        }
        collector.EndTrack(open, end);
    }

    private static MidiImport Build(MidiCollector collector, int ticksPerQuarter)
    {
        MidiTickClock clock = new(collector.Tempos, ticksPerQuarter);
        if (clock.Seconds(collector.LastTick) > MidiLimits.MaxSeconds)
        {
            throw new MidiFormatException($"The MIDI file lasts longer than {MidiLimits.MaxSeconds} seconds.");
        }
        List<MidiImportedNote> notes = ConvertNotes(collector.Notes, clock);
        double length = Math.Max(MidiClip.MinLength, clock.Seconds(collector.LastTick));
        foreach (MidiImportedNote note in notes)
        {
            length = Math.Max(length, note.Start + note.Duration);
        }
        if (length > MidiLimits.MaxSeconds)
        {
            throw new MidiFormatException($"The MIDI file lasts longer than {MidiLimits.MaxSeconds} seconds.");
        }
        (double bpm, List<MidiTempoEvent> changes) = ConvertTempo(clock);
        return new MidiImport
        {
            Bpm = bpm,
            TempoChanges = changes,
            TimeSignature = collector.Signature is { } signature ? ConvertSignature(signature) : null,
            Notes = notes,
            ControlChanges = ConvertControls(collector.Controls, clock),
            Length = length,
            IgnoredNoteOffs = collector.IgnoredNoteOffs,
            UnclosedNotes = collector.UnclosedNotes,
        };
    }

    private static List<MidiImportedNote> ConvertNotes(List<MidiRawNote> raw, MidiTickClock clock)
    {
        raw.Sort((a, b) =>
        {
            int byTick = a.On.CompareTo(b.On);
            if (byTick != 0)
            {
                return byTick;
            }
            int byPitch = a.Pitch.CompareTo(b.Pitch);
            return byPitch != 0 ? byPitch : a.Order.CompareTo(b.Order);
        });
        List<MidiImportedNote> notes = new(raw.Count);
        foreach (MidiRawNote note in raw)
        {
            double start = clock.Seconds(note.On);
            double duration = Math.Max(NoteRules.MinDuration, clock.Seconds(note.Off) - start);
            notes.Add(new MidiImportedNote(note.Pitch, start, duration, note.Velocity));
        }
        return notes;
    }

    // Two events of one controller at one tick are one value, the later.
    private static List<MidiControlChange> ConvertControls(List<MidiRawControl> raw, MidiTickClock clock)
    {
        raw.Sort((a, b) => a.Tick != b.Tick ? a.Tick.CompareTo(b.Tick) : a.Order.CompareTo(b.Order));
        List<MidiControlChange> controls = new(raw.Count);
        Dictionary<int, int> atTick = [];
        long current = -1;
        foreach (MidiRawControl control in raw)
        {
            if (control.Tick != current)
            {
                current = control.Tick;
                atTick.Clear();
            }
            MidiControlChange change = new(clock.Seconds(control.Tick), control.Controller, control.Value);
            if (atTick.TryGetValue(control.Controller, out int index))
            {
                controls[index] = change;
            }
            else
            {
                atTick[control.Controller] = controls.Count;
                controls.Add(change);
            }
        }
        return controls;
    }

    // A tempo before 0.01 s is the start tempo and a change within 0.001 s of the one before replaces it, as a project's map requires.
    private static (double Bpm, List<MidiTempoEvent> Changes) ConvertTempo(MidiTickClock clock)
    {
        double bpm = clock.BpmOf(0);
        List<MidiTempoEvent> changes = [];
        for (int i = 1; i < clock.Count; i++)
        {
            double time = clock.Seconds(clock.TickOf(i));
            double tempo = clock.BpmOf(i);
            double current = changes.Count > 0 ? changes[^1].Bpm : bpm;
            if (time < MinChangeTime)
            {
                bpm = tempo;
            }
            else if (changes.Count > 0 && time - changes[^1].Time < MinChangeSpacing)
            {
                changes[^1] = changes[^1] with { Bpm = tempo };
                double before = changes.Count > 1 ? changes[^2].Bpm : bpm;
                if (changes[^1].Bpm == before)
                {
                    changes.RemoveAt(changes.Count - 1);
                }
            }
            else if (tempo != current)
            {
                changes.Add(new MidiTempoEvent(time, tempo));
            }
        }
        return (bpm, changes);
    }

    private static TimeSignature? ConvertSignature((int Numerator, int Power) signature)
    {
        if (signature.Numerator is < 1 or > MaxNumerator || signature.Power > LargestDenominatorPower)
        {
            return null;
        }
        return new TimeSignature(signature.Numerator, 1 << signature.Power);
    }
}
