using System.Globalization;

namespace AudioLab.Web.Rendering;

/// <summary>A colour as the canvas candidates need it: 8-bit channels and a fractional alpha.</summary>
public readonly record struct Rgba(byte R, byte G, byte B, double A)
{
    /// <summary>The colour at a fraction of its opacity, which is what color-mix against transparent does.</summary>
    public Rgba Scale(double factor) => this with { A = A * factor };

    /// <summary>The colour as a CSS rgba() string.</summary>
    public string ToCss() => string.Create(CultureInfo.InvariantCulture, $"rgba({R}, {G}, {B}, {A:0.###})");

    /// <summary>Parses #rrggbb, rgb(...) and rgba(...) as a computed token value is written.</summary>
    public static Rgba Parse(string text)
    {
        string value = text.Trim();
        if (value.StartsWith('#') && value.Length == 7)
        {
            return new Rgba(
                byte.Parse(value.AsSpan(1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                byte.Parse(value.AsSpan(3, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                byte.Parse(value.AsSpan(5, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                1);
        }
        int open = value.IndexOf('(', StringComparison.Ordinal);
        if (open > 0 && value.EndsWith(')'))
        {
            string[] parts = value[(open + 1)..^1].Split(',', StringSplitOptions.TrimEntries);
            if (parts.Length is 3 or 4)
            {
                double alpha = parts.Length == 4 ? double.Parse(parts[3], CultureInfo.InvariantCulture) : 1;
                return new Rgba(byte.Parse(parts[0], CultureInfo.InvariantCulture), byte.Parse(parts[1], CultureInfo.InvariantCulture), byte.Parse(parts[2], CultureInfo.InvariantCulture), alpha);
            }
        }
        throw new FormatException($"'{text}' is not a colour a token can hold.");
    }
}
