using System.Globalization;
using System.Text.RegularExpressions;

namespace AudioLab.Conventions.Tests;

/// <summary>An opaque colour as the design tokens write it (#rrggbb), reduced to the WCAG luminance the contrast rules need.</summary>
public readonly partial record struct TokenColour(double Luminance)
{
    /// <summary>Parses "#4a9df0". Returns false for anything else, including a colour with alpha.</summary>
    public static bool TryParse(string text, out TokenColour colour)
    {
        Match match = Hex().Match(text.Trim());
        if (!match.Success)
        {
            colour = default;
            return false;
        }
        double red = Linear(match.Groups[1].Value);
        double green = Linear(match.Groups[2].Value);
        double blue = Linear(match.Groups[3].Value);
        colour = new TokenColour((0.2126 * red) + (0.7152 * green) + (0.0722 * blue));
        return true;
    }

    /// <summary>WCAG contrast ratio between two colours, from 1 (identical) to 21 (black on white).</summary>
    public static double Contrast(TokenColour first, TokenColour second)
    {
        double lighter = Math.Max(first.Luminance, second.Luminance);
        double darker = Math.Min(first.Luminance, second.Luminance);
        return (lighter + 0.05) / (darker + 0.05);
    }

    private static double Linear(string channel)
    {
        double value = int.Parse(channel, NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255.0;
        return value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
    }

    [GeneratedRegex("^#([0-9a-fA-F]{2})([0-9a-fA-F]{2})([0-9a-fA-F]{2})$")]
    private static partial Regex Hex();
}
