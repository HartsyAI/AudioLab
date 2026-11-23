namespace AudioLab.Audio.Midi;

/// <summary>A controller event as the file has it, in ticks.</summary>
/// <param name="Tick">The tick it is on.</param>
/// <param name="Controller">The controller number.</param>
/// <param name="Value">The value.</param>
/// <param name="Order">Where it came among all controller events, so that two at one tick keep the file's order.</param>
internal readonly record struct MidiRawControl(long Tick, int Controller, int Value, int Order);
