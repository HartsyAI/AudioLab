using System.Globalization;
using System.Text.RegularExpressions;

namespace AudioLab.Core.Model;

/// <summary>Collects what a validation finds, with the shared checks. Past the cap it only counts.</summary>
internal sealed partial class ProblemList
{
    private readonly List<string> _items = [];

    private int _more;

    public void Add(string message)
    {
        if (_items.Count < ProjectLimits.MaxProblems)
        {
            _items.Add(message);
        }
        else
        {
            _more++;
        }
    }

    public IReadOnlyList<string> ToList()
    {
        return _more == 0 ? _items : [.. _items, $"and {_more} more."];
    }

    /// <summary>A number from <paramref name="min"/> to <paramref name="max"/>; NaN and infinity never pass.</summary>
    public void Range(double value, double min, double max, string path)
    {
        if (!(value >= min && value <= max))
        {
            string shown = double.IsFinite(value) ? value.ToString(CultureInfo.InvariantCulture) : "a non-finite number";
            Add($"{path}: {shown} must be from {Format(min)} to {Format(max)}.");
        }
    }

    /// <summary>A number above zero and at most <paramref name="max"/>.</summary>
    public void Positive(double value, double max, string path)
    {
        if (!(value > 0 && value <= max))
        {
            string shown = double.IsFinite(value) ? value.ToString(CultureInfo.InvariantCulture) : "a non-finite number";
            Add($"{path}: {shown} must be above 0 and at most {Format(max)}.");
        }
    }

    /// <summary>Text of 1 to <paramref name="max"/> characters; empty is fine when not <paramref name="required"/>.</summary>
    public void Text(string? value, int max, string path, bool required = true)
    {
        if (value is null || value.Length > max || (required && string.IsNullOrWhiteSpace(value)))
        {
            Add($"{path}: must be {(required ? "1" : "0")} to {max} characters.");
        }
    }

    /// <summary>A colour as #rgb, #rrggbb or #rrggbbaa; nothing else is safe to put in a style.</summary>
    public void Color(string? value, string path)
    {
        if (value is null || !ColorPattern().IsMatch(value))
        {
            Add($"{path}: a colour is #rgb, #rrggbb or #rrggbbaa.");
        }
    }

    public void Id(string? id, string path)
    {
        if (!IdRules.IsValid(id))
        {
            Add($"{path}: an id is 1 to {IdRules.MaxLength} letters, digits, underscores or hyphens.");
        }
    }

    /// <summary>A well-formed id that no earlier call has seen; true when it is new.</summary>
    public bool Unique(HashSet<string> seen, string? id, string path)
    {
        if (!IdRules.IsValid(id))
        {
            Id(id, path);
            return false;
        }
        if (!seen.Add(id!))
        {
            Add($"{path}: the id {id} is used more than once.");
            return false;
        }
        return true;
    }

    public void Defined<TEnum>(TEnum value, string path)
        where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            Add($"{path}: {value} is not a known {typeof(TEnum).Name}.");
        }
    }

    private static string Format(double value)
    {
        return value switch
        {
            double.MaxValue => "the largest number",
            double.Epsilon => "the smallest above 0",
            _ => value.ToString(CultureInfo.InvariantCulture),
        };
    }

    [GeneratedRegex("^#([0-9a-fA-F]{3}|[0-9a-fA-F]{6}|[0-9a-fA-F]{8})$", RegexOptions.CultureInvariant)]
    private static partial Regex ColorPattern();
}
