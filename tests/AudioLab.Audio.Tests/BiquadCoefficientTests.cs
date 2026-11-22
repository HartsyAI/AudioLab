namespace AudioLab.Audio.Tests;

public sealed class BiquadCoefficientTests
{
    private static double Dc(BiquadCoefficients c) => (c.B0 + c.B1 + c.B2) / (1 + c.A1 + c.A2);

    private static double Nyquist(BiquadCoefficients c) => (c.B0 - c.B1 + c.B2) / (1 - c.A1 + c.A2);

    [Fact]
    public void ALowShelfHasItsGainAtDirectCurrentAndNoneAtNyquist()
    {
        BiquadCoefficients shelf = BiquadCoefficients.LowShelf(200, 12, 48000);

        Assert.Equal(Math.Pow(10, 12.0 / 20), Dc(shelf), 6);
        Assert.Equal(1, Nyquist(shelf), 6);
    }

    [Fact]
    public void AHighShelfHasItsGainAtNyquistAndNoneAtDirectCurrent()
    {
        BiquadCoefficients shelf = BiquadCoefficients.HighShelf(4000, -9, 48000);

        Assert.Equal(Math.Pow(10, -9.0 / 20), Nyquist(shelf), 6);
        Assert.Equal(1, Dc(shelf), 6);
    }

    [Fact]
    public void APeakAndAFlatBandChangeNothingAtTheEdgesOfTheSpectrum()
    {
        BiquadCoefficients peak = BiquadCoefficients.Peaking(1000, 6, 1, 48000);

        Assert.Equal(1, Dc(peak), 6);
        Assert.Equal(1, Nyquist(peak), 6);
        Assert.Equal(BiquadCoefficients.Identity, BiquadCoefficients.Peaking(1000, 0, 1, 48000));
    }

    [Fact]
    public void ALowPassPassesDirectCurrentAndAHighPassBlocksIt()
    {
        Assert.Equal(1, Dc(BiquadCoefficients.LowPass(1000, 0.707, 48000)), 6);
        Assert.Equal(0, Dc(BiquadCoefficients.HighPass(1000, 0.707, 48000)), 6);
    }
}
