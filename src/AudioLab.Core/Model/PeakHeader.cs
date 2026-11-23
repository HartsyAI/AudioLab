namespace AudioLab.Core.Model;

/// <summary>What the header and level table of a peak file say.</summary>
/// <param name="Channels">Channels of the media file.</param>
/// <param name="SampleRate">Frames a second of the media file.</param>
/// <param name="Frames">Frames in the media file.</param>
/// <param name="Levels">Each level's frames a peak and its peak count, finest first.</param>
/// <param name="TableEnd">Where the values of the first level start.</param>
/// <param name="TotalBytes">How long the whole file must be.</param>
public sealed record PeakHeader(
    int Channels,
    int SampleRate,
    long Frames,
    IReadOnlyList<(int FramesPerPeak, long Peaks)> Levels,
    int TableEnd,
    long TotalBytes);
