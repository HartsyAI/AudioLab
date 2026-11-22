namespace AudioLab.Audio;

/// <summary>Plays a clip in a loop, resampling by linear interpolation to the engine's rate.</summary>
public sealed class SamplePlayer : ITrackSource
{
    private const float ToUnit = 1f / 32768f;

    private readonly short[] _samples;

    private readonly int _frames;

    private readonly double _step;

    private double _position;

    /// <summary>Creates a player for a clip at the engine's sample rate.</summary>
    public SamplePlayer(SampleClip clip, int sampleRate)
    {
        ArgumentNullException.ThrowIfNull(clip);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sampleRate);
        _samples = clip.Samples;
        _frames = clip.Frames;
        _step = (double)clip.SampleRate / sampleRate;
    }

    /// <inheritdoc />
    public void Render(Span<float> left, Span<float> right)
    {
        double position = _position;
        for (int i = 0; i < left.Length; i++)
        {
            int index = (int)position;
            float fraction = (float)(position - index);
            int next = index + 1 == _frames ? 0 : index + 1;
            float l0 = _samples[index * 2];
            float r0 = _samples[(index * 2) + 1];
            float l1 = _samples[next * 2];
            float r1 = _samples[(next * 2) + 1];
            left[i] = (l0 + ((l1 - l0) * fraction)) * ToUnit;
            right[i] = (r0 + ((r1 - r0) * fraction)) * ToUnit;
            position += _step;
            if (position >= _frames)
            {
                position -= _frames;
            }
        }
        _position = position;
    }
}
