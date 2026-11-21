using System.Globalization;
using AudioLab.Web.Interop;

namespace AudioLab.Web.Rendering;

/// <summary>The design tokens C# needs to draw, read from the root element's computed style so tokens.css stays the only declaration.</summary>
public sealed class TokenSet
{
    private static readonly string[] _colourNames =
    [
        "--surface-lane", "--surface-panel", "--surface-well", "--line", "--lane-line", "--grid-line", "--grid-line-strong",
        "--tick", "--text", "--text-soft", "--text-faint", "--accent", "--record", "--ink", "--playhead",
        "--track-1", "--track-2", "--track-3", "--track-4", "--track-5", "--track-6",
        "--track-7", "--track-8", "--track-9", "--track-10", "--track-11", "--track-12",
    ];

    private static readonly string[] _lengthNames =
    [
        "--lane-height", "--ruler-row", "--header-column", "--radius-xs", "--radius-sm", "--space-4", "--space-16", "--font-size-label",
    ];

    private readonly Dictionary<string, Rgba> _colours = [];

    private readonly Dictionary<string, double> _lengths = [];

    private TokenSet()
    {
    }

    /// <summary>Reads every token the painters use.</summary>
    public static TokenSet Read(IBrowser browser)
    {
        TokenSet set = new();
        foreach (string name in _colourNames)
        {
            set._colours[name] = Rgba.Parse(browser.RootToken(name));
        }
        double root = browser.RootFontSize;
        foreach (string name in _lengthNames)
        {
            set._lengths[name] = ParseLength(browser.RootToken(name), root);
        }
        return set;
    }

    /// <summary>The colour of a token.</summary>
    public Rgba Colour(string name) => _colours[name];

    /// <summary>The length of a token in CSS pixels.</summary>
    public double Length(string name) => _lengths[name];

    private static double ParseLength(string text, double rootFontSize)
    {
        if (text.EndsWith("rem", StringComparison.Ordinal))
        {
            return double.Parse(text[..^3], CultureInfo.InvariantCulture) * rootFontSize;
        }
        if (text.EndsWith("px", StringComparison.Ordinal))
        {
            return double.Parse(text[..^2], CultureInfo.InvariantCulture);
        }
        throw new FormatException($"'{text}' is not a length a token can hold.");
    }
}
