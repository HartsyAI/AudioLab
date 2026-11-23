using System.Globalization;

namespace AudioLab.Web.Session;

/// <summary>Writes numbers into CSS the way CSS reads them: a point for the decimal separator, whatever the culture of the reader.</summary>
/// <remarks>A number written with the current culture reads <c>6,5rem</c> in German, which the browser drops, so lengths go through here.</remarks>
public static class Css
{
    /// <summary>A length in pixels, such as <c>12.5px</c>, with at most two decimals.</summary>
    /// <param name="pixels">The length.</param>
    /// <returns>The value with its unit.</returns>
    public static string Px(double pixels) => Length(pixels, "px");

    /// <summary>A length in root font sizes, such as <c>6.5rem</c>, with at most two decimals.</summary>
    /// <param name="rem">The length.</param>
    /// <returns>The value with its unit.</returns>
    public static string Rem(double rem) => Length(rem, "rem");

    /// <summary>A percentage, such as <c>42.5%</c>, with at most two decimals.</summary>
    /// <param name="percent">The value in percent.</param>
    /// <returns>The value with its sign.</returns>
    public static string Percent(double percent) => Length(percent, "%");

    /// <summary>A plain number, such as <c>12.5</c>, for an attribute like the width of an SVG, with at most two decimals.</summary>
    /// <param name="value">The number.</param>
    /// <returns>The number as text.</returns>
    public static string Number(double value) => Length(value, string.Empty);

    private static string Length(double value, string unit)
    {
        return string.Create(CultureInfo.InvariantCulture, $"{value:0.##}{unit}");
    }
}
