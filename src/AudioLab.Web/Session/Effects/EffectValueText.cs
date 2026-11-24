using System.Globalization;
using AudioLab.Core.Rules;

namespace AudioLab.Web.Session.Effects;

/// <summary>How an effect setting's value is written beside its control: the unit is Core's, and a number reads the same in every culture.</summary>
public static class EffectValueText
{
    /// <summary>The value as written, such as "250 ms", "-24 dB", "4:1", "30%" or "1.5 kHz".</summary>
    public static string Of(EffectParameter setting, double value)
    {
        ArgumentNullException.ThrowIfNull(setting);
        CultureInfo culture = CultureInfo.InvariantCulture;
        return setting.Unit switch
        {
            EffectUnit.Hertz => value >= 1000 ? $"{Trim(value / 1000, 2).ToString(culture)} kHz" : $"{Trim(value, 1).ToString(culture)} Hz",
            EffectUnit.Decibel => $"{Signed(Trim(value, 1), setting.Min < 0, culture)} dB",
            EffectUnit.Seconds => value < 1 ? $"{Trim(value * 1000, 1).ToString(culture)} ms" : $"{Trim(value, 2).ToString(culture)} s",
            EffectUnit.Ratio => $"{Trim(value, 1).ToString(culture)}:1",
            EffectUnit.Fraction => $"{Trim(value * 100, 0).ToString(culture)}%",
            _ => Trim(value, 2).ToString(culture),
        };
    }

    // A value that rounds to nothing is 0, never -0.
    private static double Trim(double value, int decimals)
    {
        double rounded = Math.Round(value, decimals, MidpointRounding.AwayFromZero);
        return rounded == 0 ? 0 : rounded;
    }

    private static string Signed(double value, bool showPlus, CultureInfo culture)
    {
        return value > 0 && showPlus ? "+" + value.ToString(culture) : value.ToString(culture);
    }
}
