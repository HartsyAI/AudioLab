namespace AudioLab.Audio;

/// <summary>An FFT of real input of a fixed size N, done as a complex FFT of half the size.</summary>
/// <remarks>
/// Spectra hold the N/2 + 1 bins from 0 up to Nyquist; the rest follow by symmetry. The object keeps its own scratch space, so
/// one caller at a time.
/// </remarks>
internal sealed class RealFft
{
    private readonly int _size;
    private readonly int _half;
    private readonly Fft _fft;
    private readonly float[] _cos;
    private readonly float[] _sin;
    private readonly float[] _workReal;
    private readonly float[] _workImaginary;

    public RealFft(int size)
    {
        _size = size;
        _half = size / 2;
        _fft = new Fft(_half);
        _cos = new float[_half + 1];
        _sin = new float[_half + 1];
        for (int k = 0; k <= _half; k++)
        {
            _cos[k] = (float)Math.Cos(2 * Math.PI * k / size);
            _sin[k] = (float)-Math.Sin(2 * Math.PI * k / size);
        }
        _workReal = new float[_half];
        _workImaginary = new float[_half];
    }

    /// <summary>The number of bins of a spectrum: from direct current up to Nyquist.</summary>
    public int Bins => _half + 1;

    /// <summary>Transforms <paramref name="input"/>, of the full size, into the spectrum in the two output arrays.</summary>
    public void Forward(float[] input, float[] real, float[] imaginary)
    {
        for (int n = 0; n < _half; n++)
        {
            _workReal[n] = input[2 * n];
            _workImaginary[n] = input[(2 * n) + 1];
        }
        _fft.Transform(_workReal, _workImaginary, inverse: false);
        for (int k = 0; k <= _half; k++)
        {
            int a = k == _half ? 0 : k;
            int b = k == 0 ? 0 : _half - k;
            float ar = _workReal[a];
            float ai = _workImaginary[a];
            float br = _workReal[b];
            float bi = -_workImaginary[b];
            float evenReal = (ar + br) / 2;
            float evenImaginary = (ai + bi) / 2;
            float oddReal = (ai - bi) / 2;
            float oddImaginary = -(ar - br) / 2;
            real[k] = evenReal + ((_cos[k] * oddReal) - (_sin[k] * oddImaginary));
            imaginary[k] = evenImaginary + ((_cos[k] * oddImaginary) + (_sin[k] * oddReal));
        }
    }

    /// <summary>Turns a spectrum back into <paramref name="output"/>, of the full size.</summary>
    public void Inverse(float[] real, float[] imaginary, float[] output)
    {
        for (int k = 0; k < _half; k++)
        {
            float xr = real[k];
            float xi = imaginary[k];
            float mr = real[_half - k];
            float mi = -imaginary[_half - k];
            float evenReal = (xr + mr) / 2;
            float evenImaginary = (xi + mi) / 2;
            float differenceReal = (xr - mr) / 2;
            float differenceImaginary = (xi - mi) / 2;
            float oddReal = (_cos[k] * differenceReal) + (_sin[k] * differenceImaginary);
            float oddImaginary = (_cos[k] * differenceImaginary) - (_sin[k] * differenceReal);
            _workReal[k] = evenReal - oddImaginary;
            _workImaginary[k] = evenImaginary + oddReal;
        }
        _fft.Transform(_workReal, _workImaginary, inverse: true);
        for (int n = 0; n < _half; n++)
        {
            output[2 * n] = _workReal[n];
            output[(2 * n) + 1] = _workImaginary[n];
        }
    }
}
