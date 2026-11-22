using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Audio;

/// <summary>A feedback delay as the old graph wired it, free-running or following the tempo.</summary>
/// <remarks>
/// The delay line feeds a low-pass tone filter, whose output is both the wet signal and the feedback. A synced delay follows the
/// tempo steps, and every change of time glides over about 15 ms, worked out per sample from the frame.
/// </remarks>
internal sealed class DelayInsert : IInsert
{
    // The old DAW smoothed a retuned delay with a 15 ms time constant.
    private const double GlideSeconds = 0.015;
    // A Butterworth low-pass has no peak, so the loop never gains; the old graph set 0.707 dB, which peaks 1.7 dB and let a high feedback grow.
    private const double ToneQDb = -3.0103;
    private const double MaxTailSeconds = 30;
    private const double SilenceDb = -60;

    private readonly int _sampleRate;
    private readonly TempoStep[] _steps;
    private readonly string _division;
    private readonly bool _sync;
    private readonly double _time;
    private readonly float _feedback;
    private readonly float _dry;
    private readonly float _wet;
    private readonly double _glide;
    private readonly float[] _lineLeft;
    private readonly float[] _lineRight;
    private readonly Biquad _toneLeft;
    private readonly Biquad _toneRight;
    private int _write;
    private int _step;
    private double _delay;
    private bool _primed;

    public DelayInsert(DelayEffect effect, InsertContext context)
    {
        _sampleRate = context.SampleRate;
        _steps = [.. context.TempoSteps];
        _sync = effect.Sync;
        _division = effect.Division;
        _time = effect.Time;
        _feedback = (float)Math.Min(0.9, effect.Feedback);
        _dry = (float)(1 - effect.Mix);
        _wet = (float)effect.Mix;
        _glide = 1 - Math.Exp(-1 / (GlideSeconds * _sampleRate));
        int length = (int)Math.Ceiling(DelayLimitSeconds * _sampleRate) + 4;
        _lineLeft = new float[length];
        _lineRight = new float[length];
        BiquadCoefficients tone = BiquadCoefficients.LowPass(effect.Tone, ToneQDb, _sampleRate);
        _toneLeft = new Biquad(tone);
        _toneRight = new Biquad(tone);
        TailSeconds = TailFor(effect, context);
    }

    /// <summary>The longest delay time, which is also the longest synced one.</summary>
    public static double DelayLimitSeconds => 4;

    public double TailSeconds { get; }

    public void Reset()
    {
        Array.Clear(_lineLeft);
        Array.Clear(_lineRight);
        _toneLeft.Reset();
        _toneRight.Reset();
        _write = 0;
        _primed = false;
    }

    public void Process(Span<float> left, Span<float> right, long startFrame)
    {
        if (!_primed)
        {
            _step = StepAt(startFrame);
            _delay = TargetFrames(_step);
            _primed = true;
        }
        for (int i = 0; i < left.Length; i++)
        {
            long frame = startFrame + i;
            while (_step + 1 < _steps.Length && _steps[_step + 1].Frame <= frame)
            {
                _step++;
            }
            double target = TargetFrames(_step);
            _delay += (target - _delay) * _glide;
            float echoLeft = _toneLeft.Process(Read(_lineLeft, _delay));
            float echoRight = _toneRight.Process(Read(_lineRight, _delay));
            _lineLeft[_write] = left[i] + (echoLeft * _feedback);
            _lineRight[_write] = right[i] + (echoRight * _feedback);
            _write = _write + 1 == _lineLeft.Length ? 0 : _write + 1;
            left[i] = (left[i] * _dry) + (echoLeft * _wet);
            right[i] = (right[i] * _dry) + (echoRight * _wet);
        }
    }

    // The time, in frames, that the tempo step asks for; a plain delay ignores the tempo.
    private double TargetFrames(int step)
    {
        double seconds = _sync ? EffectRules.SyncedDelayTime(_division, _steps[step].Bpm) : _time;
        return Math.Min(DelayLimitSeconds, seconds) * _sampleRate;
    }

    private int StepAt(long frame)
    {
        int step = 0;
        while (step + 1 < _steps.Length && _steps[step + 1].Frame <= frame)
        {
            step++;
        }
        return step;
    }

    // Reads the line a fractional number of frames behind the write position, by linear interpolation.
    private float Read(float[] line, double delayFrames)
    {
        double position = _write - delayFrames;
        if (position < 0)
        {
            position += line.Length;
        }
        if (position >= line.Length)
        {
            position -= line.Length;
        }
        int index = (int)position;
        float fraction = (float)(position - index);
        int next = index + 1 == line.Length ? 0 : index + 1;
        return (line[index] * (1 - fraction)) + (line[next] * fraction);
    }

    // The echoes fall by the feedback each time, so the delay rings until they are 60 dB down.
    private static double TailFor(DelayEffect effect, InsertContext context)
    {
        double longest = effect.Sync
            ? context.TempoSteps.Max(step => EffectRules.SyncedDelayTime(effect.Division, step.Bpm))
            : effect.Time;
        longest = Math.Min(DelayLimitSeconds, longest);
        double feedback = Math.Min(0.9, effect.Feedback);
        double repeats = feedback < 1e-3 ? 0 : SilenceDb / (20 * Math.Log10(feedback));
        return Math.Min(MaxTailSeconds, longest * (1 + repeats));
    }
}
