namespace AudioLab.Audio;

/// <summary>A fixed-gain all-pass section, one building block of the reverb.</summary>
internal sealed class AllpassFilter(int length)
{
    private readonly float[] _buffer = new float[length];

    private int _index;

    /// <summary>Runs one sample through the section.</summary>
    public float Process(float input)
    {
        float stored = _buffer[_index];
        _buffer[_index] = input + (stored * 0.5f);
        _index++;
        if (_index == _buffer.Length)
        {
            _index = 0;
        }
        return stored - input;
    }
}
