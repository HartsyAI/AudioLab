namespace AudioLab.Audio.Midi;

/// <summary>A note read from a file: no id yet, which the clip it goes into gives it.</summary>
/// <param name="Pitch">The MIDI note number, 0 to 127.</param>
/// <param name="Start">Where it starts, in seconds from the start of the file.</param>
/// <param name="Duration">How long it sounds, in seconds, at least <see cref="AudioLab.Core.Rules.NoteRules.MinDuration"/>.</param>
/// <param name="Velocity">How hard it is struck, 1 to 127.</param>
public readonly record struct MidiImportedNote(int Pitch, double Start, double Duration, int Velocity);
