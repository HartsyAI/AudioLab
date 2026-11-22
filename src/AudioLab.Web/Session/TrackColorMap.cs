using System.Globalization;

namespace AudioLab.Web.Session;

/// <summary>Maps a colour the document stores (#rgb, #rrggbb or #rrggbbaa) to the nearest of the twelve track colours, as the design says the UI does.</summary>
/// <remarks>The twelve values are read from the style sheet once at start, so <c>tokens.css</c> stays the only place a colour is declared.</remarks>
public sealed class TrackColorMap
{
    private const int Tokens = 12;

    private readonly (int Red, int Green, int Blue)[] _palette;

    private readonly Dictionary<string, int> _cache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Creates the map from the values of <c>--track-1</c> to <c>--track-12</c>.</summary>
    /// <exception cref="ArgumentException">There are not twelve values, or one is not a hex colour.</exception>
    public TrackColorMap(IReadOnlyList<string> trackTokenValues)
    {
        ArgumentNullException.ThrowIfNull(trackTokenValues);
        if (trackTokenValues.Count != Tokens)
        {
            throw new ArgumentException($"The twelve track colours are needed, not {trackTokenValues.Count}.", nameof(trackTokenValues));
        }
        _palette = [.. trackTokenValues.Select(value => TryParse(value, out (int, int, int) rgb) ? rgb : throw new ArgumentException($"'{value}' is not a colour.", nameof(trackTokenValues)))];
    }

    /// <summary>The 1-based number of the track colour nearest to a stored colour; an unreadable colour is the first.</summary>
    public int Nearest(string? color)
    {
        if (color is null || !TryParse(color, out (int Red, int Green, int Blue) wanted))
        {
            return 1;
        }
        if (_cache.TryGetValue(color, out int known))
        {
            return known;
        }
        int best = 0;
        long bestDistance = long.MaxValue;
        for (int i = 0; i < _palette.Length; i++)
        {
            long distance = Square(wanted.Red - _palette[i].Red) + Square(wanted.Green - _palette[i].Green) + Square(wanted.Blue - _palette[i].Blue);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = i;
            }
        }
        _cache[color] = best + 1;
        return best + 1;
    }

    /// <summary>The style value that paints with the track colour nearest to a stored colour.</summary>
    public string Token(string? color) => $"var(--track-{Nearest(color)})";

    private static long Square(int value) => (long)value * value;

    private static bool TryParse(string text, out (int Red, int Green, int Blue) rgb)
    {
        rgb = default;
        string hex = text.Trim();
        if (hex.Length == 0 || hex[0] != '#')
        {
            return false;
        }
        hex = hex[1..];
        if (hex.Length is 3)
        {
            hex = string.Concat(hex.Select(digit => new string(digit, 2)));
        }
        if (hex.Length is not (6 or 8) || !int.TryParse(hex[..6], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int value))
        {
            return false;
        }
        rgb = ((value >> 16) & 0xFF, (value >> 8) & 0xFF, value & 0xFF);
        return true;
    }
}
