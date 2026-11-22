namespace AudioLab.Audio;

/// <summary>Something that produces a track's stereo signal, one block at a time.</summary>
public interface ITrackSource
{
    /// <summary>Overwrites both buffers, which have the same length, with the next samples.</summary>
    public void Render(Span<float> left, Span<float> right);
}
