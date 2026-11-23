namespace AudioLab.Web.Waveforms;

/// <summary>Chooses which zoom level of a peak file a clip is drawn from.</summary>
public static class WaveformLevel
{
    /// <summary>The coarsest level whose frames a peak are at most the frames a pixel covers, or the finest when the view is closer than that.</summary>
    /// <remarks>Coarser would blur the picture; finer would load and scan more than a pixel can show.</remarks>
    /// <param name="framesPerPeak">The levels' frames a peak, finest first.</param>
    /// <param name="sampleRate">Frames a second of the media file.</param>
    /// <param name="pixelsPerSecond">How many pixels a second of the timeline takes.</param>
    public static int For(IReadOnlyList<int> framesPerPeak, int sampleRate, double pixelsPerSecond)
    {
        ArgumentNullException.ThrowIfNull(framesPerPeak);
        ArgumentOutOfRangeException.ThrowIfZero(framesPerPeak.Count);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(sampleRate, 0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(pixelsPerSecond, 0);
        double framesPerPixel = sampleRate / pixelsPerSecond;
        int chosen = 0;
        for (int index = 1; index < framesPerPeak.Count; index++)
        {
            if (framesPerPeak[index] <= framesPerPixel)
            {
                chosen = index;
            }
        }
        return chosen;
    }
}
