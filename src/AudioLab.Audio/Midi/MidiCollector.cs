namespace AudioLab.Audio.Midi;

/// <summary>Gathers what the tracks of a file hold, in ticks, and holds the limits on how much it may be.</summary>
internal sealed class MidiCollector
{
    private int _noteOns;
    private (long Tick, int Numerator, int Power)? _signature;

    /// <summary>The notes that have ended.</summary>
    public List<MidiRawNote> Notes { get; } = [];

    /// <summary>The controller events.</summary>
    public List<MidiRawControl> Controls { get; } = [];

    /// <summary>The tempo events, with microseconds a quarter note.</summary>
    public List<(long Tick, int Microseconds)> Tempos { get; } = [];

    /// <summary>The last tick of any track, where its End of Track is.</summary>
    public long LastTick { get; private set; }

    /// <summary>How many note-offs had no note to end.</summary>
    public int IgnoredNoteOffs { get; private set; }

    /// <summary>How many notes were closed by the end of their track.</summary>
    public int UnclosedNotes { get; private set; }

    /// <summary>The earliest time signature, as numerator and power of two of the denominator.</summary>
    public (int Numerator, int Power)? Signature => _signature is { } found ? (found.Numerator, found.Power) : null;

    /// <summary>Starts a note, which waits for its note-off.</summary>
    public void NoteOn(Dictionary<int, Queue<(long Tick, int Velocity, int Order)>> open, MidiMessage message)
    {
        if (_noteOns >= MidiLimits.MaxNotes)
        {
            throw new MidiFormatException($"The MIDI file holds more than {MidiLimits.MaxNotes} notes.");
        }
        int key = (message.Channel * 128) + message.Data1;
        if (!open.TryGetValue(key, out Queue<(long Tick, int Velocity, int Order)>? queue))
        {
            queue = new Queue<(long Tick, int Velocity, int Order)>();
            open[key] = queue;
        }
        queue.Enqueue((message.Tick, message.Data2, _noteOns++));
    }

    /// <summary>Ends the oldest note of that pitch and channel that is still sounding, or counts the event as stray.</summary>
    public void NoteOff(Dictionary<int, Queue<(long Tick, int Velocity, int Order)>> open, MidiMessage message)
    {
        int key = (message.Channel * 128) + message.Data1;
        if (open.TryGetValue(key, out Queue<(long Tick, int Velocity, int Order)>? queue) && queue.Count > 0)
        {
            (long tick, int velocity, int order) = queue.Dequeue();
            Notes.Add(new MidiRawNote(tick, message.Tick, message.Data1, velocity, order));
        }
        else
        {
            IgnoredNoteOffs++;
        }
    }

    /// <summary>Keeps a controller event.</summary>
    public void Controller(MidiMessage message)
    {
        if (Controls.Count >= MidiLimits.MaxControlChanges)
        {
            throw new MidiFormatException($"The MIDI file holds more than {MidiLimits.MaxControlChanges} controller events.");
        }
        Controls.Add(new MidiRawControl(message.Tick, message.Data1, message.Data2, Controls.Count));
    }

    /// <summary>Keeps a tempo event.</summary>
    public void Tempo(MidiMessage message)
    {
        if (Tempos.Count >= MidiLimits.MaxTempoEvents)
        {
            throw new MidiFormatException($"The MIDI file holds more than {MidiLimits.MaxTempoEvents} tempo events.");
        }
        Tempos.Add((message.Tick, message.Data1));
    }

    /// <summary>Keeps the earliest time signature.</summary>
    public void TimeSignature(MidiMessage message)
    {
        if (_signature is not { } found || message.Tick < found.Tick)
        {
            _signature = (message.Tick, message.Data1, message.Data2);
        }
    }

    /// <summary>Closes the notes of a track that were never ended at the track's last tick.</summary>
    public void EndTrack(Dictionary<int, Queue<(long Tick, int Velocity, int Order)>> open, long end)
    {
        LastTick = Math.Max(LastTick, end);
        foreach (KeyValuePair<int, Queue<(long Tick, int Velocity, int Order)>> entry in open)
        {
            foreach ((long tick, int velocity, int order) in entry.Value)
            {
                Notes.Add(new MidiRawNote(tick, end, entry.Key % 128, velocity, order));
                UnclosedNotes++;
            }
        }
    }
}
