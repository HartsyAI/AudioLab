using System.Text.RegularExpressions;

namespace AudioLab.Core.Model;

/// <summary>The rule for what a track icon may be: a short text such as an emoji, or a small embedded image.</summary>
public static partial class TrackIcon
{
    /// <summary>The longest text icon, in characters: room for a joined emoji sequence, not for words.</summary>
    public const int MaxTextLength = 16;

    /// <summary>The longest image icon, as a data URL, in characters.</summary>
    public const int MaxImageLength = 60000;

    /// <summary>Whether <paramref name="icon"/> may be stored: short text, or a small png, jpeg or webp data URL.</summary>
    public static bool IsValid(string icon)
    {
        ArgumentNullException.ThrowIfNull(icon);
        if (icon.StartsWith("data:", StringComparison.Ordinal))
        {
            return icon.Length <= MaxImageLength && ImagePattern().IsMatch(icon);
        }
        return icon.Length > 0 && icon.Length <= MaxTextLength;
    }

    [GeneratedRegex(@"\Adata:image/(png|jpeg|webp);base64,[A-Za-z0-9+/]+=*\z", RegexOptions.CultureInvariant)]
    private static partial Regex ImagePattern();
}
