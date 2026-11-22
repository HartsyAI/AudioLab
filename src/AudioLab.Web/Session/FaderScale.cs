using System.Globalization;

namespace AudioLab.Web.Session;

/// <summary>The taper of every volume control, one scale for the header slider and the channel fader: five marked decibel values at even steps, as the fader's scale reads.</summary>
public static class FaderScale
{
    private static readonly double[] _marks = [-48, -24, -12, 0, 6];

    /// <summary>The decibel values written beside the fader, from the top down.</summary>
    public static IReadOnlyList<string> Labels { get; } = ["+6", "0", "-12", "-24", "-48"];

    /// <summary>The decibels of a linear gain, with silence as negative infinity.</summary>
    public static double Decibels(double gain) => gain <= 0 ? double.NegativeInfinity : 20 * Math.Log10(gain);

    /// <summary>Where a gain sits on the scale, from 0 at the bottom mark to 1 at the top mark; past either end it stays on the end.</summary>
    public static double Position(double gain)
    {
        double decibels = Decibels(gain);
        if (decibels <= _marks[0])
        {
            return 0;
        }
        if (decibels >= _marks[^1])
        {
            return 1;
        }
        for (int i = 1; i < _marks.Length; i++)
        {
            if (decibels <= _marks[i])
            {
                double within = (decibels - _marks[i - 1]) / (_marks[i] - _marks[i - 1]);
                return (i - 1 + within) / (_marks.Length - 1);
            }
        }
        return 1;
    }

    /// <summary>The readout of a gain: one decimal and a sign for a boost, or "-inf" for silence.</summary>
    public static string Text(double gain)
    {
        double decibels = Decibels(gain);
        if (double.IsNegativeInfinity(decibels) || decibels < -99.95)
        {
            return "-inf";
        }
        string text = decibels.ToString("0.0", CultureInfo.InvariantCulture);
        return decibels >= 0.05 ? "+" + text : text == "-0.0" ? "0.0" : text;
    }
}
