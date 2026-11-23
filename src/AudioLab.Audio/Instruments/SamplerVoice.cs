namespace AudioLab.Audio.Instruments;

/// <summary>One note of the sampler: the sample read at a pitch ratio with cubic interpolation, under an attack and a release.</summary>
/// <remarks>
/// The sample is read straight at the ratio, so a note above the root has the aliasing of that and one below it has none; at the root
/// itself a sample point is returned as it is. A voice ends where its sample does, held or not. A one-shot ignores its note's end and
/// plays to the end of the sample, or to <see cref="SamplerSettings.OneShotLimit"/> seconds after the note, fading out over the last
/// <see cref="SamplerSettings.LimitFade"/> seconds of that.
/// </remarks>
internal sealed class SamplerVoice : InstrumentVoice
{
    private readonly SamplerSettings _settings;
    private double _position;
    private double _ratio;
    private double _level;
    private double _envelope;
    private double _fadeStep;
    private long _age;
    private long _offFrame;
    private long _limitFrame;
    private bool _attacking;

    /// <summary>Makes a free voice that plays the settings' sample.</summary>
    public SamplerVoice(SamplerSettings settings) => _settings = settings;

    /// <inheritdoc />
    public override double Level => _envelope;

    /// <inheritdoc />
    public override void Start(long startFrame, long offFrame, int pitch, float velocity, long elapsed)
    {
        _ratio = _settings.Ratio(pitch);
        _position = elapsed * _ratio;
        _level = _settings.Gain * velocity;
        _envelope = 0;
        _age = 0;
        _attacking = true;
        StartFrame = startFrame;
        _offFrame = offFrame;
        _limitFrame = Math.Max(offFrame, startFrame) + _settings.LimitFrames;
        State = VoiceState.Held;
    }

    /// <inheritdoc />
    public override void Take()
    {
        _fadeStep = _envelope / _settings.FadeFrames;
        State = VoiceState.Fading;
    }

    /// <inheritdoc />
    public override void Render(long startFrame, int count, Span<float> left, Span<float> right)
    {
        PcmAudio sample = _settings.Sample;
        ReadOnlySpan<float> first = sample.Channel(0);
        bool mono = sample.ChannelCount == 1;
        ReadOnlySpan<float> second = mono ? first : sample.Channel(1);
        for (int i = 0; i < count && State != VoiceState.Free; i++)
        {
            long frame = startFrame + i;
            if (State == VoiceState.Held && !_settings.OneShot && frame >= _offFrame)
            {
                State = VoiceState.Released;
            }
            if (_position >= first.Length || frame >= _limitFrame)
            {
                State = VoiceState.Free;
                break;
            }
            double gain = _envelope * LimitGain(frame);
            float one = Interpolate(first, _position);
            float other = mono ? one : Interpolate(second, _position);
            left[i] += (float)(one * gain);
            right[i] += (float)(other * gain);
            _position += _ratio;
            _age++;
            Advance();
        }
    }

    // A one-shot that is cut at its limit fades out over the last stretch, so the cut does not click; other voices have no limit.
    private double LimitGain(long frame)
    {
        if (!_settings.OneShot)
        {
            return 1;
        }
        long left = _limitFrame - frame;
        return left >= _settings.LimitFadeFrames ? 1 : (double)left / _settings.LimitFadeFrames;
    }

    // Four points through the position, the Catmull-Rom spline; outside the sample is silence, and a whole position is the sample's own.
    private static float Interpolate(ReadOnlySpan<float> samples, double position)
    {
        int index = (int)position;
        float fraction = (float)(position - index);
        if (fraction == 0)
        {
            return samples[index];
        }
        float y0 = index > 0 ? samples[index - 1] : 0;
        float y1 = samples[index];
        float y2 = index + 1 < samples.Length ? samples[index + 1] : 0;
        float y3 = index + 2 < samples.Length ? samples[index + 2] : 0;
        float c1 = 0.5f * (y2 - y0);
        float c2 = y0 - (2.5f * y1) + (2 * y2) - (0.5f * y3);
        float c3 = (0.5f * (y3 - y0)) + (1.5f * (y1 - y2));
        return (((((c3 * fraction) + c2) * fraction) + c1) * fraction) + y1;
    }

    private void Advance()
    {
        switch (State)
        {
            case VoiceState.Held:
                if (_attacking)
                {
                    _attacking = _age < _settings.AttackFrames;
                    _envelope = _attacking ? _level * _age / _settings.AttackFrames : _level;
                }
                break;
            case VoiceState.Released:
                _envelope *= _settings.ReleaseCoefficient;
                if (_envelope < SynthSettings.SilentLevel)
                {
                    State = VoiceState.Free;
                }
                break;
            default:
                _envelope -= _fadeStep;
                if (_envelope <= 0)
                {
                    _envelope = 0;
                    State = VoiceState.Free;
                }
                break;
        }
    }
}
