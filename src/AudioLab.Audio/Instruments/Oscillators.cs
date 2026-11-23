using AudioLab.Core.Model;

namespace AudioLab.Audio.Instruments;

/// <summary>The oscillator shapes of the synth, as a function of a phase in [0, 1) and the phase step of one sample.</summary>
/// <remarks>
/// Every wave starts at phase 0 as the Web Audio oscillator the old DAW used does: a sine, a triangle and a sawtooth at zero going up,
/// a square at its high level. The sawtooth and the square are band-limited with PolyBLEP, which removes the aliasing of their jumps;
/// the triangle is not, since its harmonics fall off as the square of their number. No oscillator uses a random number.
/// </remarks>
internal static class Oscillators
{
    /// <summary>The sample of a wave at <paramref name="phase"/>, between -1 and 1 and a little past either for a band-limited jump.</summary>
    public static double Sample(OscillatorWave wave, double phase, double step) => wave switch
    {
        OscillatorWave.Sine => Math.Sin(2 * Math.PI * phase),
        OscillatorWave.Triangle => Triangle(phase),
        OscillatorWave.Square => Square(phase, step),
        _ => Sawtooth(phase, step),
    };

    private static double Triangle(double phase) => phase switch
    {
        < 0.25 => 4 * phase,
        < 0.75 => 2 - (4 * phase),
        _ => (4 * phase) - 4,
    };

    // Zero at phase 0, rising to 1 at the half cycle, where it jumps to -1.
    private static double Sawtooth(double phase, double step)
    {
        double shifted = phase + 0.5;
        shifted -= shifted >= 1 ? 1 : 0;
        return (2 * shifted) - 1 - Blep(shifted, step);
    }

    private static double Square(double phase, double step)
    {
        double shifted = phase + 0.5;
        shifted -= shifted >= 1 ? 1 : 0;
        return (phase < 0.5 ? 1 : -1) + Blep(phase, step) - Blep(shifted, step);
    }

    // The correction of a unit upward jump at phase 0, spread over the sample before it and the sample after.
    private static double Blep(double phase, double step)
    {
        if (phase < step)
        {
            double t = phase / step;
            return t + t - (t * t) - 1;
        }
        if (phase > 1 - step)
        {
            double t = (phase - 1) / step;
            return (t * t) + t + t + 1;
        }
        return 0;
    }
}
