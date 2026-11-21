namespace AudioLab.Core.Model;

/// <summary>A MIDI controller event, such as the sustain pedal (controller 64).</summary>
/// <param name="Time">Where it happens, in seconds from the clip start.</param>
/// <param name="Controller">The controller number, 0 to 127.</param>
/// <param name="Value">The controller value, 0 to 127.</param>
public readonly record struct MidiControlChange(double Time, int Controller, int Value);
