using System.Text.RegularExpressions;

namespace AudioLab.Core.Model;

/// <summary>The rule for a stored colour: #rgb, #rrggbb or #rrggbbaa. Nothing else is safe to put in a style.</summary>
public static partial class ProjectColor
{
    /// <summary>Whether <paramref name="color"/> is a well-formed colour.</summary>
    public static bool IsValid(string? color) => color is not null && Pattern().IsMatch(color);

    [GeneratedRegex(@"\A#([0-9a-fA-F]{3}|[0-9a-fA-F]{6}|[0-9a-fA-F]{8})\z", RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();
}
