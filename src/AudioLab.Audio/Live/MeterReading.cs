using AudioLab.Core.Model;

namespace AudioLab.Audio.Live;

/// <summary>The levels the engine's mix put out since the last reading.</summary>
/// <param name="Tracks">The tracks the peaks are of, in order.</param>
/// <param name="Peaks">A left and a right peak for each track, then the master's left and right, as linear levels where 1 is full scale.</param>
public readonly record struct MeterReading(IReadOnlyList<TrackId> Tracks, float[] Peaks);
