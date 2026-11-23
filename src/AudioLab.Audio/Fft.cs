namespace AudioLab.Audio;

/// <summary>An in-place radix-2 complex FFT of a fixed power-of-two size, with its tables built once.</summary>
internal sealed class Fft
{
    private readonly int _size;
    private readonly float[] _cos;
    private readonly float[] _sin;
    private readonly int[] _reverse;

    public Fft(int size)
    {
        if (size < 2 || (size & (size - 1)) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(size), "The size must be a power of two of at least 2.");
        }
        _size = size;
        _cos = new float[size / 2];
        _sin = new float[size / 2];
        for (int i = 0; i < size / 2; i++)
        {
            _cos[i] = (float)Math.Cos(2 * Math.PI * i / size);
            _sin[i] = (float)-Math.Sin(2 * Math.PI * i / size);
        }
        _reverse = new int[size];
        int bits = System.Numerics.BitOperations.Log2((uint)size);
        for (int i = 0; i < size; i++)
        {
            int reversed = 0;
            for (int bit = 0; bit < bits; bit++)
            {
                reversed |= ((i >> bit) & 1) << (bits - 1 - bit);
            }
            _reverse[i] = reversed;
        }
    }

    /// <summary>Transforms in place; the inverse is scaled by one over the size.</summary>
    public void Transform(float[] real, float[] imaginary, bool inverse)
    {
        for (int i = 0; i < _size; i++)
        {
            int j = _reverse[i];
            if (j > i)
            {
                (real[i], real[j]) = (real[j], real[i]);
                (imaginary[i], imaginary[j]) = (imaginary[j], imaginary[i]);
            }
        }
        float sign = inverse ? -1 : 1;
        for (int half = 1; half < _size; half <<= 1)
        {
            int stride = _size / (2 * half);
            for (int start = 0; start < _size; start += 2 * half)
            {
                for (int k = 0; k < half; k++)
                {
                    float wr = _cos[k * stride];
                    float wi = sign * _sin[k * stride];
                    int a = start + k;
                    int b = a + half;
                    float tr = (real[b] * wr) - (imaginary[b] * wi);
                    float ti = (real[b] * wi) + (imaginary[b] * wr);
                    real[b] = real[a] - tr;
                    imaginary[b] = imaginary[a] - ti;
                    real[a] += tr;
                    imaginary[a] += ti;
                }
            }
        }
        if (inverse)
        {
            float scale = 1f / _size;
            for (int i = 0; i < _size; i++)
            {
                real[i] *= scale;
                imaginary[i] *= scale;
            }
        }
    }
}
