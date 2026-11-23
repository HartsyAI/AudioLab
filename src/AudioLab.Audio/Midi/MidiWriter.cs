using System.Buffers;
using System.Buffers.Binary;
using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Audio.Midi;

/// <summary>Writes one MIDI clip as a format 0 Standard MIDI File, through the project's tempo map, cut at the clip's end.</summary>
internal static class MidiWriter
{
    private const int PedalController = 64;
    private const int PedalDownFrom = 64;
    private const double TempoEpsilon = 1e-6;

    /// <summary>Writes the clip, whose times are seconds from its start; the tempo map is the project's, from the project's start.</summary>
    public static byte[] Write(MidiClip clip, double bpm, IReadOnlyList<TempoChange> changes, TimeSignature signature)
    {
        ArgumentNullException.ThrowIfNull(clip);
        ArgumentNullException.ThrowIfNull(changes);
        if (!double.IsFinite(clip.Length) || clip.Length <= 0)
        {
            throw new ArgumentException("The clip has no length.", nameof(clip));
        }
        int power = DenominatorPower(signature);
        // The file opens at the tempo in force where the clip starts.
        double startBpm = TempoMap.From(bpm, changes).BpmAt(clip.Start + TempoEpsilon);
        List<TempoChange> shifted = ShiftedChanges(changes, clip.Start, clip.Length);
        TempoMap map = TempoMap.From(startBpm, shifted);
        List<(MidiMessage Message, int Order)> events = [];
        AddTempo(events, map, startBpm, shifted);
        events.Add((new MidiMessage(0, MidiMessageKind.TimeSignature, 0, signature.Numerator, power), events.Count));
        AddNotes(events, clip, map);
        AddControls(events, clip, map);
        events.Sort(Compare);
        long end = Math.Max(Tick(map, clip.Length), events[^1].Message.Tick);
        ReleasePedal(events, end);
        return Assemble(events, end);
    }

    // A change at or after the clip's end would lengthen the file, so it is left out.
    private static List<TempoChange> ShiftedChanges(IReadOnlyList<TempoChange> changes, double start, double length) =>
        [.. changes.Where(change => change.Time - start > TempoEpsilon && change.Time - start < length)
            .Select(change => new TempoChange(change.Id, change.Time - start, change.Bpm))];

    private static int DenominatorPower(TimeSignature signature)
    {
        int power = signature.Denominator is >= 1 and <= 32 ? System.Numerics.BitOperations.Log2((uint)signature.Denominator) : -1;
        if (signature.Numerator is < 1 or > 64 || power < 0 || (1 << power) != signature.Denominator)
        {
            throw new ArgumentException(
                $"The time signature {signature.Numerator}/{signature.Denominator} is not one a project holds.",
                nameof(signature));
        }
        return power;
    }

    private static void AddTempo(List<(MidiMessage Message, int Order)> events, TempoMap map, double baseBpm, List<TempoChange> shifted)
    {
        events.Add((new MidiMessage(0, MidiMessageKind.Tempo, 0, Microseconds(baseBpm), 0), events.Count));
        foreach (TempoChange change in shifted.Where(change => double.IsFinite(change.Time) && double.IsFinite(change.Bpm) && change.Bpm > 0)
                     .OrderBy(change => change.Time))
        {
            events.Add((new MidiMessage(Tick(map, change.Time), MidiMessageKind.Tempo, 0, Microseconds(change.Bpm), 0), events.Count));
        }
    }

    private static int Microseconds(double bpm)
    {
        double microseconds = Math.Round(60_000_000.0 / bpm);
        if (microseconds > MidiLimits.MaxMicrosecondsPerQuarter || microseconds < MidiLimits.MinMicrosecondsPerQuarter)
        {
            throw new MidiFormatException($"A tempo of {bpm} beats a minute is outside the 3.58 to 1000 that a MIDI file can hold.");
        }
        return (int)microseconds;
    }

    // Velocity 0 would be a note-off, so such a note is left out; a note is at least as long as the reader makes it.
    private static void AddNotes(List<(MidiMessage Message, int Order)> events, MidiClip clip, TempoMap map)
    {
        List<(long On, long Off, MidiNote Note)> notes = [];
        foreach (MidiNote note in clip.Notes)
        {
            if (note.Pitch is < 0 or > 127 || note.Velocity is < 0 or > 127 || !double.IsFinite(note.Start) || !double.IsFinite(note.Duration))
            {
                throw new ArgumentException($"Note {note.Id} is not a valid note.", nameof(clip));
            }
            if (note.Start < 0 || note.Start >= clip.Length || note.Velocity == 0)
            {
                continue;
            }
            long on = Tick(map, note.Start);
            double end = Math.Min(note.Start + Math.Max(note.Duration, NoteRules.MinDuration), clip.Length);
            notes.Add((on, Math.Max(on + 1, Tick(map, end)), note));
        }
        // The reader ends the oldest open note of a pitch first, so of two that start together the shorter must start first.
        notes.Sort(CompareNotes);
        foreach ((long on, long off, MidiNote note) in notes)
        {
            events.Add((new MidiMessage(on, MidiMessageKind.NoteOn, 0, note.Pitch, note.Velocity), events.Count));
            events.Add((new MidiMessage(off, MidiMessageKind.NoteOff, 0, note.Pitch, 0), events.Count));
        }
    }

    private static int CompareNotes((long On, long Off, MidiNote Note) a, (long On, long Off, MidiNote Note) b)
    {
        int result = a.On.CompareTo(b.On);
        result = result != 0 ? result : a.Note.Pitch.CompareTo(b.Note.Pitch);
        return result != 0 ? result : a.Off.CompareTo(b.Off);
    }

    private static void AddControls(List<(MidiMessage Message, int Order)> events, MidiClip clip, TempoMap map)
    {
        foreach (MidiControlChange change in clip.ControlChanges)
        {
            if (change.Controller is < 0 or > 127 || change.Value is < 0 or > 127 || !double.IsFinite(change.Time))
            {
                throw new ArgumentException("A controller event is not valid.", nameof(clip));
            }
            if (change.Time >= 0 && change.Time <= clip.Length)
            {
                events.Add((new MidiMessage(Tick(map, change.Time), MidiMessageKind.Controller, 0, change.Controller, change.Value), events.Count));
            }
        }
    }

    // A pedal still down at the clip's end is let up there, so it cannot hold the notes that follow.
    private static void ReleasePedal(List<(MidiMessage Message, int Order)> events, long end)
    {
        int last = -1;
        foreach ((MidiMessage message, _) in events)
        {
            if (message.Kind == MidiMessageKind.Controller && message.Data1 == PedalController)
            {
                last = message.Data2;
            }
        }
        if (last >= PedalDownFrom)
        {
            events.Add((new MidiMessage(end, MidiMessageKind.Controller, 0, PedalController, 0), events.Count));
        }
    }

    private static byte[] Assemble(List<(MidiMessage Message, int Order)> events, long end)
    {
        ArrayBufferWriter<byte> track = new();
        long last = 0;
        foreach ((MidiMessage message, _) in events)
        {
            WriteGap(track, message.Tick - last);
            last = message.Tick;
            WriteMessage(track, message);
        }
        WriteGap(track, end - last);
        Put(track, [0xFF, 0x2F, 0x00]);
        if (track.WrittenCount + 22 > MidiLimits.MaxFileBytes)
        {
            throw new MidiFormatException("The clip is too large for a MIDI file.");
        }
        byte[] file = new byte[22 + track.WrittenCount];
        "MThd"u8.CopyTo(file);
        BinaryPrimitives.WriteUInt32BigEndian(file.AsSpan(4), 6);
        BinaryPrimitives.WriteUInt16BigEndian(file.AsSpan(8), 0);
        BinaryPrimitives.WriteUInt16BigEndian(file.AsSpan(10), 1);
        BinaryPrimitives.WriteUInt16BigEndian(file.AsSpan(12), MidiLimits.WrittenTicksPerQuarter);
        "MTrk"u8.CopyTo(file.AsSpan(14));
        BinaryPrimitives.WriteUInt32BigEndian(file.AsSpan(18), (uint)track.WrittenCount);
        track.WrittenSpan.CopyTo(file.AsSpan(22));
        return file;
    }

    private static int Compare((MidiMessage Message, int Order) a, (MidiMessage Message, int Order) b)
    {
        int result = a.Message.Tick.CompareTo(b.Message.Tick);
        result = result != 0 ? result : Rank(a.Message.Kind).CompareTo(Rank(b.Message.Kind));
        result = result != 0 ? result : a.Message.Data1.CompareTo(b.Message.Data1);
        return result != 0 ? result : a.Order.CompareTo(b.Order);
    }

    // Tempo and signature first, then note-offs, controllers and note-ons, so a note ending where another starts is not run into it.
    private static int Rank(MidiMessageKind kind) => kind switch
    {
        MidiMessageKind.Tempo or MidiMessageKind.TimeSignature => 0,
        MidiMessageKind.NoteOff => 1,
        MidiMessageKind.Controller => 2,
        _ => 3,
    };

    private static long Tick(TempoMap map, double seconds) =>
        (long)Math.Round(map.BeatsAt(Math.Max(0, seconds)) * MidiLimits.WrittenTicksPerQuarter, MidpointRounding.AwayFromZero);

    private static void WriteMessage(ArrayBufferWriter<byte> track, MidiMessage message)
    {
        switch (message.Kind)
        {
            case MidiMessageKind.Tempo:
                Put(track, [0xFF, 0x51, 0x03, (byte)(message.Data1 >> 16), (byte)(message.Data1 >> 8), (byte)message.Data1]);
                break;
            case MidiMessageKind.TimeSignature:
                Put(track, [0xFF, 0x58, 0x04, (byte)message.Data1, (byte)message.Data2, 24, 8]);
                break;
            case MidiMessageKind.NoteOff:
                Put(track, [0x80, (byte)message.Data1, 0]);
                break;
            case MidiMessageKind.NoteOn:
                Put(track, [0x90, (byte)message.Data1, (byte)message.Data2]);
                break;
            default:
                Put(track, [0xB0, (byte)message.Data1, (byte)message.Data2]);
                break;
        }
    }

    private static void Put(ArrayBufferWriter<byte> track, ReadOnlySpan<byte> bytes) => track.Write(bytes);

    // A gap that one delta cannot hold is crossed with empty text events, which a reader skips.
    private static void WriteGap(ArrayBufferWriter<byte> track, long delta)
    {
        while (delta > MidiLimits.MaxVariableLength)
        {
            WriteVariableLength(track, MidiLimits.MaxVariableLength);
            Put(track, [0xFF, 0x01, 0x00]);
            delta -= MidiLimits.MaxVariableLength;
        }
        WriteVariableLength(track, delta);
    }

    private static void WriteVariableLength(ArrayBufferWriter<byte> track, long value)
    {
        if (value is < 0 or > MidiLimits.MaxVariableLength)
        {
            throw new InvalidOperationException("A gap was not split to fit a delta.");
        }
        Span<byte> bytes = stackalloc byte[4];
        int count = 0;
        bytes[count++] = (byte)(value & 0x7F);
        while ((value >>= 7) > 0)
        {
            bytes[count++] = (byte)((value & 0x7F) | 0x80);
        }
        for (int i = count - 1; i >= 0; i--)
        {
            Put(track, [bytes[i]]);
        }
    }
}
