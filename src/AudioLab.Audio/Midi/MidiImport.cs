using AudioLab.Core.Model;

namespace AudioLab.Audio.Midi;

/// <summary>What a Standard MIDI File holds, in seconds, ready to become a MIDI clip and a tempo map.</summary>
public sealed record MidiImport
{
    /// <summary>The tempo at the start, in beats per minute: the file's first tempo when it is at the start, else 120.</summary>
    public required double Bpm { get; init; }

    /// <summary>The tempo changes after the start, in time order.</summary>
    public required IReadOnlyList<MidiTempoEvent> TempoChanges { get; init; }

    /// <summary>The first time signature of the file, or null when it has none or one that a project cannot hold.</summary>
    public TimeSignature? TimeSignature { get; init; }

    /// <summary>The notes of every track and channel together, ordered by start, then pitch.</summary>
    public required IReadOnlyList<MidiImportedNote> Notes { get; init; }

    /// <summary>The controller events of every track and channel together, in time order, one for each time and controller.</summary>
    public required IReadOnlyList<MidiControlChange> ControlChanges { get; init; }

    /// <summary>How long the file lasts, in seconds: to its last End of Track or the end of its last note, never under 0.05.</summary>
    public required double Length { get; init; }

    /// <summary>How many note-off events had no note to end, which were left out.</summary>
    public int IgnoredNoteOffs { get; init; }

    /// <summary>How many notes were never ended and so last to the end of their track.</summary>
    public int UnclosedNotes { get; init; }

    /// <summary>A MIDI clip of the notes and controllers, with ids from 1 in the order of the notes, placed at <paramref name="start"/>.</summary>
    public MidiClip ToClip(ClipId id, string name, double start)
    {
        ArgumentNullException.ThrowIfNull(name);
        MidiNote[] notes = new MidiNote[Notes.Count];
        for (int i = 0; i < notes.Length; i++)
        {
            MidiImportedNote note = Notes[i];
            notes[i] = new MidiNote(new NoteId(i + 1), note.Pitch, note.Start, note.Duration, note.Velocity);
        }
        return new MidiClip
        {
            Id = id,
            Name = name,
            Start = start,
            Length = Length,
            NextNoteId = new NoteId(notes.Length + 1),
            Notes = new ValueList<MidiNote>(notes),
            ControlChanges = new ValueList<MidiControlChange>(ControlChanges),
            Muted = false,
        };
    }
}
