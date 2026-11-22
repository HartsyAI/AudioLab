namespace AudioLab.Core.Model;

/// <summary>The peaks of one media file at every zoom level, finest first: what a waveform is drawn from.</summary>
/// <param name="Channels">Channels of the file.</param>
/// <param name="SampleRate">Frames a second of the file.</param>
/// <param name="Frames">Frames in the file.</param>
/// <param name="Levels">The levels, each coarser than the one before.</param>
public sealed record PeakData(int Channels, int SampleRate, long Frames, IReadOnlyList<PeakLevel> Levels);
