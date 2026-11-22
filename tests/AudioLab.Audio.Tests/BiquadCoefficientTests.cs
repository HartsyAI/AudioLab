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

    private static double MagnitudeDb(BiquadCoefficients c, double hertz, int rate)
    {
        double omega = 2 * Math.PI * hertz / rate;
        System.Numerics.Complex z1 = System.Numerics.Complex.FromPolarCoordinates(1, -omega);
        System.Numerics.Complex z2 = z1 * z1;
        System.Numerics.Complex h = (c.B0 + (c.B1 * z1) + (c.B2 * z2)) / (1 + (c.A1 * z1) + (c.A2 * z2));
        return 20 * Math.Log10(h.Magnitude);
    }

    [Fact]
    public void APeakHasItsGainAtItsCentreAndNoneFarFromIt()
    {
        BiquadCoefficients peak = BiquadCoefficients.Peaking(1000, 6, 2, 48000);

        Assert.Equal(6, MagnitudeDb(peak, 1000, 48000), 6);
        Assert.True(Math.Abs(MagnitudeDb(peak, 50, 48000)) < 0.1);
        Assert.True(Math.Abs(MagnitudeDb(peak, 20000, 48000)) < 0.1);
    }

    [Fact]
    public void AShelfHasHalfItsGainInDecibelsAtItsCorner()
    {
        Assert.Equal(6, MagnitudeDb(BiquadCoefficients.LowShelf(200, 12, 48000), 200, 48000), 6);
        Assert.Equal(-4.5, MagnitudeDb(BiquadCoefficients.HighShelf(4000, -9, 48000), 4000, 48000), 6);
    }

    [Fact]
    public void ThePassFiltersHaveTheirResonanceAtTheCornerAndFallAboutTwelveDecibelsAnOctave()
    {
        BiquadCoefficients resonant = BiquadCoefficients.LowPass(1000, 0.707, 48000);
        BiquadCoefficients flat = BiquadCoefficients.HighPass(1000, -3.0103, 48000);

        Assert.Equal(0.707, MagnitudeDb(resonant, 1000, 48000), 3);
        Assert.Equal(-3.0103, MagnitudeDb(flat, 1000, 48000), 3);
        Assert.InRange(MagnitudeDb(BiquadCoefficients.LowPass(1000, -3.0103, 48000), 8000, 48000), -40, -35);
        Assert.InRange(MagnitudeDb(flat, 125, 48000), -40, -35);
    }
}
