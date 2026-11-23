namespace AudioLab.Audio.Instruments;

/// <summary>One note of the synth: two oscillators through a low-pass filter whose cutoff sweeps, under an amplitude envelope.</summary>
/// <remarks>
/// Allocation-free once built. Everything it does depends on the frames since its own start, never on how a render is cut into blocks:
/// the filter is recalculated every <see cref="SynthSettings.ControlInterval"/> frames counted from the voice's start.
/// </remarks>
internal sealed class SynthVoice(SynthSettings settings) : InstrumentVoice
{
    private double _phase1;
    private double _phase2;
    private double _step1;
    private double _step2;
    private bool _osc1On;
    private bool _osc2On;
    private double _level;
    private double _envelope;
    private double _fadeStep;
    private long _age;
    private long _offFrame;
    private bool _attacking;
    private BiquadCoefficients _filter = BiquadCoefficients.Identity;
    private double _s1;
    private double _s2;

    /// <inheritdoc />
    public override double Level => _envelope;

    /// <inheritdoc />
    public override void Start(long startFrame, long offFrame, int pitch, float velocity, long elapsed)
    {
        double frequency = SynthSettings.Frequency(pitch);
        int rate = settings.SampleRate;
        _step1 = frequency * settings.Detune1 / rate;
        _step2 = frequency * settings.Ratio2 * settings.Detune2 / rate;
        _osc1On = _step1 < 0.5;
        _osc2On = settings.Level2 > 0 && _step2 < 0.5;
        _phase1 = 0;
        _phase2 = 0;
        _level = settings.Gain * velocity;
        _envelope = 0;
        _age = 0;
        _attacking = true;
        _s1 = 0;
        _s2 = 0;
        StartFrame = startFrame - elapsed;
        _offFrame = offFrame;
        State = VoiceState.Held;
    }

    /// <inheritdoc />
    public override void Take()
    {
        _fadeStep = _envelope / settings.FadeFrames;
        State = VoiceState.Fading;
    }

    /// <inheritdoc />
    public override void Render(long startFrame, int count, Span<float> left, Span<float> right)
    {
        for (int i = 0; i < count && State != VoiceState.Free; i++)
        {
            if (State == VoiceState.Held && startFrame + i >= _offFrame)
            {
                State = VoiceState.Released;
            }
            if ((_age & (SynthSettings.ControlInterval - 1)) == 0)
            {
                UpdateFilter();
            }
            float sample = (float)(Filter(Oscillate()) * _envelope);
            left[i] += sample;
            right[i] += sample;
            _age++;
            Advance();
        }
    }

    private double Oscillate()
    {
        double sum = 0;
        if (_osc1On)
        {
            sum += Oscillators.Sample(settings.Osc1, _phase1, _step1) * settings.Level1;
            _phase1 += _step1;
            _phase1 -= _phase1 >= 1 ? 1 : 0;
        }
        if (_osc2On)
        {
            sum += Oscillators.Sample(settings.Osc2, _phase2, _step2) * settings.Level2;
            _phase2 += _step2;
            _phase2 -= _phase2 >= 1 ? 1 : 0;
        }
        return sum;
    }

    private double Filter(double input)
    {
        BiquadCoefficients c = _filter;
        double output = (c.B0 * input) + _s1;
        _s1 = (c.B1 * input) - (c.A1 * output) + _s2;
        _s2 = (c.B2 * input) - (c.A2 * output);
        return output;
    }

    // The cutoff is a function of the voice's age only, so a sweep is the same whatever block it is rendered in.
    private void UpdateFilter()
    {
        double baseCutoff = settings.FilterBase;
        double peak = settings.FilterPeak;
        if (peak == baseCutoff && _age > 0)
        {
            return;
        }
        double cutoff;
        if (_age < settings.AttackFrames)
        {
            cutoff = baseCutoff + ((peak - baseCutoff) * _age / settings.AttackFrames);
        }
        else
        {
            double target = baseCutoff + ((peak - baseCutoff) * settings.Sustain);
            cutoff = target + ((peak - target) * Math.Exp(-(_age - settings.AttackFrames) / settings.DecayTauFrames));
        }
        _filter = BiquadCoefficients.LowPass(cutoff, settings.ResonanceDb, settings.SampleRate);
    }

    // The envelope for the next frame. A release goes on from the level the attack or decay had reached, so a note
    // let go during its attack does not jump (audit row 20).
    private void Advance()
    {
        switch (State)
        {
            case VoiceState.Held:
                AdvanceHeld();
                break;
            case VoiceState.Released:
                _envelope *= settings.ReleaseCoefficient;
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

    private void AdvanceHeld()
    {
        if (_attacking)
        {
            if (_age >= settings.AttackFrames)
            {
                _attacking = false;
                _envelope = _level;
            }
            else
            {
                _envelope = _level * _age / settings.AttackFrames;
            }
            return;
        }
        double hold = _level * settings.Sustain;
        _envelope = hold + ((_envelope - hold) * settings.DecayCoefficient);
        if (hold < SynthSettings.SilentLevel && _envelope < SynthSettings.SilentLevel)
        {
            // A voice that has decayed to nothing is over, so it neither counts against the polyphony nor ends in subnormal numbers.
            _envelope = 0;
            State = VoiceState.Free;
        }
    }
}
