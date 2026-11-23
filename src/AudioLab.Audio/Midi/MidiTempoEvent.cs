namespace AudioLab.Audio.Midi;

/// <summary>A change of tempo read from a file.</summary>
/// <param name="Time">Where it starts, in seconds from the start of the file, at least 0.01 and 0.001 from the change before it.</param>
/// <param name="Bpm">The tempo from there on, in beats per minute.</param>
public readonly record struct MidiTempoEvent(double Time, double Bpm);
