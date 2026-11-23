namespace AudioLab.Audio.Midi;

/// <summary>One event of a track, with its time in ticks from the start of the track.</summary>
/// <param name="Tick">Where the event is.</param>
/// <param name="Kind">What it is.</param>
/// <param name="Channel">The channel of a channel event, 0 to 15.</param>
/// <param name="Data1">A pitch or a controller number, or a tempo in microseconds, or a numerator.</param>
/// <param name="Data2">A velocity or a controller value, or the power of two of a denominator.</param>
internal readonly record struct MidiMessage(long Tick, MidiMessageKind Kind, int Channel, int Data1, int Data2);
