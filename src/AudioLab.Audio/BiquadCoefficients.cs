namespace AudioLab.Audio;

/// <summary>The coefficients of one second-order filter section, with the first denominator coefficient normalised to one.</summary>
/// <remarks>The formulas are those of the Web Audio biquad filter; the resonance of a pass filter is in decibels.</remarks>
internal readonly record struct BiquadCoefficients(double B0, double B1, double B2, double A1, double A2)
{
    /// <summary>A section that passes the signal unchanged.</summary>
    public static BiquadCoefficients Identity { get; } = new(1, 0, 0, 0, 0);

    /// <summary>A low-pass section; <paramref name="qDb"/> is the resonance in decibels, as Web Audio defines it.</summary>
    public static BiquadCoefficients LowPass(double frequency, double qDb, int sampleRate)
    {
        (double sin, double cos) = Trig(frequency, sampleRate);
        double alpha = sin / (2 * Math.Pow(10, qDb / 20));
        return Normalise((1 - cos) / 2, 1 - cos, (1 - cos) / 2, 1 + alpha, -2 * cos, 1 - alpha);
    }

    /// <summary>A high-pass section; <paramref name="qDb"/> is the resonance in decibels, as Web Audio defines it.</summary>
    public static BiquadCoefficients HighPass(double frequency, double qDb, int sampleRate)
    {
        (double sin, double cos) = Trig(frequency, sampleRate);
        double alpha = sin / (2 * Math.Pow(10, qDb / 20));
        return Normalise((1 + cos) / 2, -(1 + cos), (1 + cos) / 2, 1 + alpha, -2 * cos, 1 - alpha);
    }

    /// <summary>A peaking section of a gain in decibels and a width <paramref name="q"/>.</summary>
    public static BiquadCoefficients Peaking(double frequency, double gainDb, double q, int sampleRate)
    {
        if (gainDb == 0)
        {
            return Identity;
        }
        (double sin, double cos) = Trig(frequency, sampleRate);
        double alpha = sin / (2 * q);
        double a = Math.Pow(10, gainDb / 40);
        return Normalise(1 + (alpha * a), -2 * cos, 1 - (alpha * a), 1 + (alpha / a), -2 * cos, 1 - (alpha / a));
    }

    /// <summary>A low-shelf section of a gain in decibels, with unit shelf slope.</summary>
    public static BiquadCoefficients LowShelf(double frequency, double gainDb, int sampleRate)
    {
        if (gainDb == 0)
        {
            return Identity;
        }
        (double sin, double cos) = Trig(frequency, sampleRate);
        double a = Math.Pow(10, gainDb / 40);
        double beta = 2 * Math.Sqrt(a) * (sin / 2 * Math.Sqrt(2));
        return Normalise(
            a * ((a + 1) - ((a - 1) * cos) + beta),
            2 * a * ((a - 1) - ((a + 1) * cos)),
            a * ((a + 1) - ((a - 1) * cos) - beta),
            (a + 1) + ((a - 1) * cos) + beta,
            -2 * ((a - 1) + ((a + 1) * cos)),
            (a + 1) + ((a - 1) * cos) - beta);
    }

    /// <summary>A high-shelf section of a gain in decibels, with unit shelf slope.</summary>
    public static BiquadCoefficients HighShelf(double frequency, double gainDb, int sampleRate)
    {
        if (gainDb == 0)
        {
            return Identity;
        }
        (double sin, double cos) = Trig(frequency, sampleRate);
        double a = Math.Pow(10, gainDb / 40);
        double beta = 2 * Math.Sqrt(a) * (sin / 2 * Math.Sqrt(2));
        return Normalise(
            a * ((a + 1) + ((a - 1) * cos) + beta),
            -2 * a * ((a - 1) + ((a + 1) * cos)),
            a * ((a + 1) + ((a - 1) * cos) - beta),
            (a + 1) - ((a - 1) * cos) + beta,
            2 * ((a - 1) - ((a + 1) * cos)),
            (a + 1) - ((a - 1) * cos) - beta);
    }

    // A frequency at or past Nyquist is held just under it, where every section is well defined.
    private static (double Sin, double Cos) Trig(double frequency, int sampleRate)
    {
        double omega = 2 * Math.PI * Math.Clamp(frequency, 1e-3, 0.49 * sampleRate) / sampleRate;
        return (Math.Sin(omega), Math.Cos(omega));
    }

    private static BiquadCoefficients Normalise(double b0, double b1, double b2, double a0, double a1, double a2) =>
        new(b0 / a0, b1 / a0, b2 / a0, a1 / a0, a2 / a0);
}
