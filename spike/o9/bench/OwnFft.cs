namespace O9;

/// <summary>The straightforward version: iterative radix-2, tables built once per size, no lock, scalar.</summary>
public sealed class OwnFft
{
    private readonly int _n;
    private readonly int[] _rev;
    private readonly float[] _cos;
    private readonly float[] _sin;
    private readonly float[] _re;
    private readonly float[] _im;

    public OwnFft(int n)
    {
        if (n < 2 || (n & (n - 1)) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(n), "a power of two is required");
        }
        _n = n;
        int bits = System.Numerics.BitOperations.Log2((uint)n);
        _rev = new int[n];
        for (int i = 0; i < n; i++)
        {
            int r = 0;
            for (int b = 0; b < bits; b++)
            {
                r |= ((i >> b) & 1) << (bits - 1 - b);
            }
            _rev[i] = r;
        }
        _cos = new float[n / 2];
        _sin = new float[n / 2];
        for (int k = 0; k < n / 2; k++)
        {
            _cos[k] = (float)Math.Cos(2 * Math.PI * k / n);
            _sin[k] = (float)Math.Sin(2 * Math.PI * k / n);
        }
        _re = new float[n];
        _im = new float[n];
    }

    /// <summary>Forward complex transform in place (e^-i convention).</summary>
    public void Transform(Span<float> re, Span<float> im)
    {
        for (int i = 0; i < _n; i++)
        {
            int j = _rev[i];
            if (j > i)
            {
                (re[i], re[j]) = (re[j], re[i]);
                (im[i], im[j]) = (im[j], im[i]);
            }
        }
        for (int len = 2; len <= _n; len <<= 1)
        {
            int half = len >> 1;
            int step = _n / len;
            for (int i = 0; i < _n; i += len)
            {
                for (int j = 0; j < half; j++)
                {
                    float wr = _cos[j * step];
                    float wi = -_sin[j * step];
                    int a = i + j;
                    int b = a + half;
                    float tr = wr * re[b] - wi * im[b];
                    float ti = wr * im[b] + wi * re[b];
                    re[b] = re[a] - tr;
                    im[b] = im[a] - ti;
                    re[a] += tr;
                    im[a] += ti;
                }
            }
        }
    }

    /// <summary>First n/2+1 bins of a real input, using this instance's buffers (no allocation).</summary>
    public void RealTransform(ReadOnlySpan<float> input, Span<float> outRe, Span<float> outIm)
    {
        input[.._n].CopyTo(_re);
        Array.Clear(_im);
        Transform(_re, _im);
        int half = _n / 2 + 1;
        _re.AsSpan(0, half).CopyTo(outRe);
        _im.AsSpan(0, half).CopyTo(outIm);
    }

    /// <summary>Reference: a direct double-precision DFT.</summary>
    public static void NaiveDft(ReadOnlySpan<float> re, ReadOnlySpan<float> im, Span<double> outRe, Span<double> outIm)
    {
        int n = re.Length;
        for (int k = 0; k < n; k++)
        {
            double sr = 0, si = 0;
            for (int t = 0; t < n; t++)
            {
                double a = 2 * Math.PI * k * t / n;
                double c = Math.Cos(a), s = Math.Sin(a);
                sr += re[t] * c + im[t] * s;
                si += im[t] * c - re[t] * s;
            }
            outRe[k] = sr;
            outIm[k] = si;
        }
    }
}
