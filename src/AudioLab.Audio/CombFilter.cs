namespace AudioLab.Audio;

/// <summary>A feedback comb with a one-pole damper, one building block of the reverb.</summary>
internal sealed class CombFilter(int length, float feedback, float damping)
{
    private readonly float[] _buffer = new float[length];

    private int _index;

    private float _store;

    /// <summary>Runs one sample through the comb.</summary>
    public float Process(float input)
    {
        float output = _buffer[_index];
        _store = (output * (1 - damping)) + (_store * damping);
        _buffer[_index] = input + (_store * feedback);
        _index++;
        if (_index == _buffer.Length)
        {
            _index = 0;
        }
        return output;
    }
}
