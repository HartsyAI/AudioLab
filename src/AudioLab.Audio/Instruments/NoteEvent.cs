namespace AudioLab.Audio.Instruments;

/// <summary>One note as an instrument plays it: where it starts and where it is let go, in timeline frames.</summary>
/// <param name="OnFrame">The frame the note starts on.</param>
/// <param name="OffFrame">The frame its release starts on, after the sustain pedal and a covering clip are counted; always after the start.</param>
/// <param name="Pitch">The MIDI note number, 0 to 127.</param>
/// <param name="Velocity">How hard it is struck, from <see cref="NoteSchedule.QuietestVelocity"/> to 1.</param>
internal readonly record struct NoteEvent(long OnFrame, long OffFrame, int Pitch, float Velocity);
