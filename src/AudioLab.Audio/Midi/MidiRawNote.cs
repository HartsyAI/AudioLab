namespace AudioLab.Audio.Midi;

/// <summary>A note as the file has it, in ticks, before the tempo turns it into seconds.</summary>
/// <param name="On">The tick it starts on.</param>
/// <param name="Off">The tick it ends on.</param>
/// <param name="Pitch">The MIDI note number.</param>
/// <param name="Velocity">The velocity, 1 to 127.</param>
/// <param name="Order">Where its note-on came among all note-ons, so that equal notes keep the file's order.</param>
internal readonly record struct MidiRawNote(long On, long Off, int Pitch, int Velocity, int Order);
