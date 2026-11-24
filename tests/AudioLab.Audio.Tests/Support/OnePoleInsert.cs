namespace AudioLab.Audio.Tests.Support;

/// <summary>A low-pass insert with one sample of state, so tests can check what the mixer does with an insert that remembers.</summary>
public sealed class OnePoleInsert : IInsert
{
    private float _left;
    private float _right;

    public double TailSeconds => 0.2;

    public void Reset()
    {
        _left = 0;
        _right = 0;
    }

    public void Process(Span<float> left, Span<float> right, long startFrame)
    {
        for (int i = 0; i < left.Length; i++)
        {
            _left += 0.2f * (left[i] - _left);
            _right += 0.2f * (right[i] - _right);
            left[i] = _left;
            right[i] = _right;
        }
    }
}
