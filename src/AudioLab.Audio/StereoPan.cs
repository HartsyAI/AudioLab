namespace AudioLab.Audio;

/// <summary>The equal-power stereo pan of the Web Audio stereo panner, which the old DAW used: unity at the centre, a side pushed across.</summary>
internal readonly struct StereoPan
{
    private readonly float _toLeft;
    private readonly float _toRight;
    private readonly bool _leftward;

    public StereoPan(double pan)
    {
        double clamped = double.IsFinite(pan) ? Math.Clamp(pan, -1, 1) : 0;
        _leftward = clamped <= 0;
        double angle = (_leftward ? clamped + 1 : clamped) * Math.PI / 2;
        _toLeft = (float)Math.Cos(angle);
        _toRight = (float)Math.Sin(angle);
    }

    public (float Left, float Right) Apply(float left, float right) =>
        _leftward ? (left + (right * _toLeft), right * _toRight) : (left * _toLeft, right + (left * _toRight));
}
