using System.Globalization;

namespace AudioLab.Core.Model;

/// <summary>Collects what a validation finds, with the shared checks. Past the cap it only counts.</summary>
internal sealed class ProblemList
{
    private readonly List<string> _items = [];

    private int _more;

    /// <summary>Records a problem, cut to a readable length; past the cap it is only counted.</summary>
    public void Add(string message)
    {
        if (_items.Count >= ProjectLimits.MaxProblems)
        {
            _more++;
            return;
        }
        _items.Add(ProjectLimits.Shorten(message));
    }

    /// <summary>A copy of the problems, with a last line saying how many more there were.</summary>
    public IReadOnlyList<string> ToList()
    {
        return _more == 0 ? [.. _items] : [.. _items, $"and {_more} more."];
    }

    /// <summary>A number from <paramref name="min"/> to <paramref name="max"/>; NaN and infinity never pass.</summary>
    public void Range(double value, double min, double max, string path)
    {
        if (!(value >= min && value <= max))
        {
            Add($"{path}: {Shown(value)} must be from {Format(min)} to {Format(max)}.");
        }
    }

    /// <summary>A number above zero and at most <paramref name="max"/>.</summary>
    public void Positive(double value, double max, string path)
    {
        if (!(value > 0 && value <= max))
        {
            Add($"{path}: {Shown(value)} must be above 0 and at most {Format(max)}.");
        }
    }

    /// <summary>A count of at most <paramref name="max"/> items.</summary>
    public void Count(int count, int max, string path)
    {
        if (count > max)
        {
            Add($"{path}: {count} items is more than the {max} a project keeps.");
        }
    }

    /// <summary>Text of 1 to <paramref name="max"/> characters (0 if not required), without control or direction marks.</summary>
    public void Text(string? value, int max, string path, bool required = true, bool allowLineBreaks = false)
    {
        if (value is null || value.Length > max || (required && string.IsNullOrWhiteSpace(value)))
        {
            Add($"{path}: must be {(required ? "1" : "0")} to {max} characters.");
        }
        else if (!TextRules.IsClean(value, allowLineBreaks))
        {
            Add($"{path}: must not hold control or text-direction characters.");
        }
    }

    /// <summary>A colour as #rgb, #rrggbb or #rrggbbaa; nothing else is safe to put in a style.</summary>
    public void Color(string? value, string path)
    {
        if (!ProjectColor.IsValid(value))
        {
            Add($"{path}: a colour is #rgb, #rrggbb or #rrggbbaa.");
        }
    }

    /// <summary>A well-formed id.</summary>
    public void Id(string? id, string path)
    {
        if (!IdRules.IsValid(id))
        {
            Add($"{path}: {IdRules.Description}");
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

    /// <summary>An enum value that is one of its named members.</summary>
    public void Defined<TEnum>(TEnum value, string path)
        where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            Add($"{path}: {value} is not a known {typeof(TEnum).Name}.");
        }
    }

    private static string Shown(double value)
    {
        return double.IsFinite(value) ? value.ToString(CultureInfo.InvariantCulture) : "a non-finite number";
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
}
