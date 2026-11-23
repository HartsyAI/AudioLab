using System.Numerics;

namespace AudioLab.Audio;

/// <summary>Arithmetic on spectra kept as separate arrays of real and imaginary parts.</summary>
internal static class SpectrumMath
{
    /// <summary>Adds the complex product of two spectra to a running sum, a vector of bins at a time.</summary>
    public static void MultiplyAdd(float[] xr, float[] xi, float[] hr, float[] hi, float[] sumReal, float[] sumImaginary)
    {
        int bins = sumReal.Length;
        int lanes = Vector<float>.Count;
        int k = 0;
        for (; k + lanes <= bins; k += lanes)
        {
            Vector<float> ar = new(xr, k);
            Vector<float> ai = new(xi, k);
            Vector<float> br = new(hr, k);
            Vector<float> bi = new(hi, k);
            (new Vector<float>(sumReal, k) + (ar * br) - (ai * bi)).CopyTo(sumReal, k);
            (new Vector<float>(sumImaginary, k) + (ar * bi) + (ai * br)).CopyTo(sumImaginary, k);
        }
        for (; k < bins; k++)
        {
            sumReal[k] += (xr[k] * hr[k]) - (xi[k] * hi[k]);
            sumImaginary[k] += (xr[k] * hi[k]) + (xi[k] * hr[k]);
        }
    }
}
