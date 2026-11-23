namespace AudioLab.Audio;

/// <summary>The result of an offline render: stereo float samples, one array per channel.</summary>
/// <param name="Format">The format of the samples.</param>
/// <param name="Left">The left channel.</param>
/// <param name="Right">The right channel, as long as the left.</param>
public sealed record RenderedAudio(AudioFormat Format, float[] Left, float[] Right)
{
    /// <summary>How many frames each channel holds.</summary>
    public int Frames => Left.Length;
}
