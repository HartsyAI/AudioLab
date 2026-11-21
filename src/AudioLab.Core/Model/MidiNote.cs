namespace AudioLab.Core.Model;

/// <summary>One note of a MIDI clip.</summary>
/// <param name="Id">The note's id, unique within its clip.</param>
/// <param name="Pitch">The MIDI note number, 0 to 127.</param>
/// <param name="Start">Where it starts, in seconds from the clip start.</param>
/// <param name="Duration">How long it sounds, in seconds.</param>
/// <param name="Velocity">How hard it is struck, 0 to 127.</param>
public readonly record struct MidiNote(NoteId Id, int Pitch, double Start, double Duration, int Velocity);
