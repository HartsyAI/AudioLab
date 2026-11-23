namespace AudioLab.Audio;

/// <summary>A short fade over the very end of a render whose tail was cut short, so that it does not stop with a click.</summary>
internal static class TailFade
{
    /// <summary>The length of the fade, in seconds.</summary>
    public const double Seconds = 0.05;

    /// <summary>The number of frames the fade runs over at a sample rate.</summary>
    public static long FramesAt(int sampleRate) => (long)(Seconds * sampleRate);

    /// <summary>Fades what falls in the last <paramref name="fadeFrames"/> frames of a render of <paramref name="total"/> frames.</summary>
    public static void Apply(Span<float> left, Span<float> right, long blockStart, long total, long fadeFrames)
    {
        long fadeStart = total - fadeFrames;
        for (int i = 0; i < left.Length; i++)
        {
            long frame = blockStart + i;
            if (frame >= fadeStart)
            {
                float gain = (float)(total - frame) / fadeFrames;
                left[i] *= gain;
                right[i] *= gain;
            }
        }
    }
}
