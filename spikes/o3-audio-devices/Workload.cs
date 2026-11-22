using System.Diagnostics;
using System.Text;

/// <summary>What a duplex callback does: emits a click train, detects it on the input, and records timing without allocating.</summary>
internal sealed class Workload
{
    private const int MaxCallbacks = 2_000_000;
    private const int MaxPulses = 4096;

    private readonly double _sampleRate;
    private readonly bool _emit;
    private readonly long _spacing;
    private readonly long _firstPulse;
    private readonly int _burstLength;
    private readonly float[] _burst;
    private readonly long[] _starts = new long[MaxPulses];
    private readonly long[] _detected = new long[MaxPulses];
    private readonly float[] _peak = new float[MaxPulses];
    private readonly long[] _callbackTicks = new long[MaxCallbacks];
    private readonly long[] _callbackCost = new long[MaxCallbacks];
    private long _frame;
    private int _callbacks;
    private int _emitted;
    private int _found;
    private int _waiting = -1;
    private float _noisePeak;
    private float _threshold;
    private long _allocFirst = -1;
    private long _allocLast;
    private long _xruns;
    private long _frameSum;
    private int _minFrames = int.MaxValue;
    private int _maxFrames;
    private double _dacMinusAdcSum;
    private long _dacMinusAdcCount;

    public Workload(double sampleRate, bool emit, double spacingSeconds = 0.3, double firstPulseSeconds = 1.0)
    {
        _sampleRate = sampleRate;
        _emit = emit;
        _spacing = (long)(sampleRate * spacingSeconds);
        _firstPulse = (long)(sampleRate * firstPulseSeconds);
        _burstLength = (int)(sampleRate * 0.002);
        _burst = new float[_burstLength];
        for (int i = 0; i < _burstLength; i++)
        {
            _burst[i] = (float)(0.5 * Math.Sin((2 * Math.PI * 3000 * i / sampleRate) + (Math.PI / 2)));
        }
    }

    public int Callbacks => _callbacks;

    public int Pulses => _found;

    public long Xruns => _xruns;

    public void NoteXrun() => _xruns++;

    public void NoteDacMinusAdc(double seconds)
    {
        _dacMinusAdcSum += seconds;
        _dacMinusAdcCount++;
    }

    /// <summary>Handles one duplex callback. Channel 0 of the input is analysed and channel 0 of the output carries the click.</summary>
    public void OnAudio(ReadOnlySpan<float> input, int inStride, Span<float> output, int outStride, int frames)
    {
        long start = Stopwatch.GetTimestamp();
        if (_allocFirst < 0 && _callbacks >= 300)
        {
            _allocFirst = GC.GetAllocatedBytesForCurrentThread();
        }

        if (_callbacks < MaxCallbacks)
        {
            _callbackTicks[_callbacks] = start;
        }

        long g = _frame;
        bool calibrating = g < _firstPulse - (long)(_sampleRate * 0.1);
        for (int i = 0; i < frames; i++)
        {
            long idx = g + i;
            if (_emit && _emitted < MaxPulses && idx >= _firstPulse + (_emitted * _spacing) && _starts[_emitted] == 0)
            {
                _starts[_emitted] = idx;
                _emitted++;
                _waiting = _emitted - 1;
            }

            float s = 0f;
            if (_emit)
            {
                for (int k = Math.Max(0, _emitted - 2); k < _emitted; k++)
                {
                    long off = idx - _starts[k];
                    if (off >= 0 && off < _burstLength)
                    {
                        s = _burst[off];
                    }
                }
            }

            output[i * outStride] = s;
            float x = input[i * inStride];
            float a = Math.Abs(x);
            if (calibrating)
            {
                if (a > _noisePeak)
                {
                    _noisePeak = a;
                }
            }
            else
            {
                if (_threshold == 0f)
                {
                    _threshold = Math.Max(0.02f, _noisePeak * 6f);
                }

                if (_waiting >= 0 && idx >= _starts[_waiting])
                {
                    if (_detected[_waiting] == 0 && a > _threshold)
                    {
                        _detected[_waiting] = idx;
                        _found++;
                    }

                    if (_detected[_waiting] != 0 && idx < _detected[_waiting] + (_burstLength * 8) && a > _peak[_waiting])
                    {
                        _peak[_waiting] = a;
                    }
                }
            }
        }

        _frame = g + frames;
        _frameSum += frames;
        if (frames < _minFrames)
        {
            _minFrames = frames;
        }

        if (frames > _maxFrames)
        {
            _maxFrames = frames;
        }

        long end = Stopwatch.GetTimestamp();
        if (_callbacks < MaxCallbacks)
        {
            _callbackCost[_callbacks] = end - start;
            _callbacks++;
        }

        _allocLast = GC.GetAllocatedBytesForCurrentThread();
    }

    public string Report(string title)
    {
        StringBuilder sb = new();
        sb.AppendLine("--- " + title);
        double tick = 1000.0 / Stopwatch.Frequency;
        int n = _callbacks;
        double nominal = n > 0 ? _frameSum / (double)n / _sampleRate * 1000 : 0;
        sb.AppendLine($"callbacks={n} frames/callback min/avg/max={_minFrames}/{(n > 0 ? _frameSum / (double)n : 0):F1}/{_maxFrames} ({nominal:F2} ms nominal) xruns reported={_xruns}");
        if (n > 2)
        {
            double[] iv = new double[n - 1];
            for (int i = 1; i < n; i++)
            {
                iv[i - 1] = (_callbackTicks[i] - _callbackTicks[i - 1]) * tick;
            }

            Array.Sort(iv);
            int late15 = iv.Count(v => v > nominal * 1.5);
            int late2 = iv.Count(v => v > nominal * 2.0);
            double mean = iv.Average();
            double sd = Math.Sqrt(iv.Sum(v => (v - mean) * (v - mean)) / iv.Length);
            sb.AppendLine($"interval ms: mean={mean:F3} sd={sd:F3} p50={P(iv, .5):F3} p99={P(iv, .99):F3} p99.9={P(iv, .999):F3} p99.99={P(iv, .9999):F3} max={iv[^1]:F3}; late>1.5x={late15} late>2x={late2}");
            double[] cost = new double[n];
            for (int i = 0; i < n; i++)
            {
                cost[i] = _callbackCost[i] * tick;
            }

            Array.Sort(cost);
            sb.AppendLine($"time inside callback ms: p50={P(cost, .5):F4} p99.9={P(cost, .999):F4} max={cost[^1]:F4}");
        }

        sb.AppendLine($"managed bytes allocated on callback thread during run: {_allocLast - _allocFirst}");
        if (_dacMinusAdcCount > 0)
        {
            sb.AppendLine($"reported (outputDacTime - inputAdcTime) mean = {_dacMinusAdcSum / _dacMinusAdcCount * 1000:F2} ms");
        }

        if (_emit)
        {
            List<double> rt = new();
            double pk = 0;
            for (int k = 0; k < _emitted; k++)
            {
                if (_detected[k] != 0)
                {
                    rt.Add((_detected[k] - _starts[k]) / _sampleRate * 1000);
                    pk += _peak[k];
                }
            }

            sb.AppendLine($"noise floor peak={_noisePeak:F5} threshold={_threshold:F5}; pulses emitted={_emitted} detected={rt.Count}");
            if (rt.Count > 0)
            {
                rt.Sort();
                double mean = rt.Average();
                double sd = Math.Sqrt(rt.Sum(v => (v - mean) * (v - mean)) / rt.Count);
                sb.AppendLine($"ROUND TRIP ms: min={rt[0]:F2} median={rt[rt.Count / 2]:F2} mean={mean:F2} sd={sd:F2} max={rt[^1]:F2}; mean detected peak={pk / rt.Count:F3}");
                sb.AppendLine("first 10: " + string.Join(", ", rt.Take(10).Select(v => v.ToString("F2"))));
            }
        }

        return sb.ToString();
    }

    private static double P(double[] sorted, double p) => sorted[Math.Min(sorted.Length - 1, (int)(p * sorted.Length))];
}
