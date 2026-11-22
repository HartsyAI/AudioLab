namespace O9;

/// <summary>The straightforward version: the same polyphase Kaiser-windowed sinc design as the HartsyInference one, one dot product per output.</summary>
public sealed class OwnResampler
{
    private readonly int _up;
    private readonly int _down;
    private readonly int _taps;
    private readonly float[] _table; // [phase * taps + k]

    public OwnResampler(int inRate, int outRate, int taps = 64)
    {
        int g = Gcd(inRate, outRate);
        _up = outRate / g;
        _down = inRate / g;
        _taps = taps;
        double cutoff = 1.0 / Math.Max(_up, _down);
        int len = taps * _up;
        double mid = (len - 1) / 2.0;
        _table = new float[len];
        for (int n = 0; n < len; n++)
        {
            double x = n - mid;
            double sinc = x == 0 ? 1.0 : Math.Sin(Math.PI * cutoff * x) / (Math.PI * cutoff * x);
            double r = 2.0 * n / (len - 1) - 1.0;
            double kaiser = I0(8.6 * Math.Sqrt(Math.Max(0.0, 1.0 - r * r))) / I0(8.6);
            double h = sinc * cutoff * kaiser * _up;
            _table[(n % _up) * taps + n / _up] = (float)h;
        }
    }

    public int OutputLength(int inputLength) => (int)(((long)inputLength * _up + _down - 1) / _down);

    public void Resample(ReadOnlySpan<float> input, Span<float> output)
    {
        int half = _taps / 2;
        for (int m = 0; m < output.Length; m++)
        {
            long t = (long)m * _down;
            int i0 = (int)(t / _up);
            ReadOnlySpan<float> row = _table.AsSpan((int)(t % _up) * _taps, _taps);
            float acc = 0f;
            for (int k = 0; k < _taps; k++)
            {
                int s = i0 - k + half;
                if ((uint)s < (uint)input.Length)
                {
                    acc += input[s] * row[k];
                }
            }
            output[m] = acc;
        }
    }

    /// <summary>The cheap alternative the O1 spike's engine used for clips: linear interpolation.</summary>
    public static void Linear(ReadOnlySpan<float> input, Span<float> output, int inRate, int outRate)
    {
        double step = (double)inRate / outRate;
        for (int m = 0; m < output.Length; m++)
        {
            double pos = m * step;
            int i = (int)pos;
            float frac = (float)(pos - i);
            float a = i < input.Length ? input[i] : 0f;
            float b = i + 1 < input.Length ? input[i + 1] : 0f;
            output[m] = a + (b - a) * frac;
        }
    }

    private static double I0(double x)
    {
        double sum = 1, term = 1, h = x / 2;
        for (int k = 1; k < 50; k++)
        {
            term *= (h / k) * (h / k);
            sum += term;
            if (term < 1e-12 * sum)
            {
                break;
            }
        }
        return sum;
    }

    private static int Gcd(int a, int b)
    {
        while (b != 0)
        {
            (a, b) = (b, a % b);
        }
        return a;
    }
}
