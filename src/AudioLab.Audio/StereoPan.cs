namespace AudioLab.Audio;

/// <summary>The pan of the Web Audio stereo panner, in its mono and its stereo form.</summary>
/// <remarks>A mono signal is shared out by a constant-power law, 3 dB down at the centre; a stereo one keeps unity at the centre.</remarks>
internal readonly struct StereoPan
{
    private readonly float _toLeft;
    private readonly float _toRight;
    private readonly bool _leftward;
    private readonly bool _mono;

    public StereoPan(double pan, bool mono)
    {
        double clamped = double.IsFinite(pan) ? Math.Clamp(pan, -1, 1) : 0;
        _mono = mono;
        _leftward = clamped <= 0;
        double angle = (mono ? (clamped + 1) / 2 : _leftward ? clamped + 1 : clamped) * Math.PI / 2;
        _toLeft = (float)Math.Cos(angle);
        _toRight = (float)Math.Sin(angle);
    }

    public (float Left, float Right) Apply(float left, float right)
    {
        if (_mono)
        {
            return (left * _toLeft, right * _toRight);
        }
        return _leftward ? (left + (right * _toLeft), right * _toRight) : (left * _toLeft, right + (left * _toRight));
    }
}
