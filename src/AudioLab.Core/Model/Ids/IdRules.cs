namespace AudioLab.Core.Model;

/// <summary>What an id may look like: 1 to 64 lower-case letters, digits, underscores or hyphens; no device names.</summary>
/// <remarks>
/// Such an id holds no separator, no dot and no upper case, so two ids never differ only by case on a case-insensitive
/// disk. A store still never builds a path from a raw id; it maps an id to a name of its own.
/// </remarks>
public static class IdRules
{
    /// <summary>The longest id, in characters.</summary>
    public const int MaxLength = 64;

    /// <summary>The rule in words, for an error message.</summary>
    public const string Description = "An id is 1 to 64 lower-case letters, digits, underscores or hyphens.";

    private static readonly HashSet<string> _reserved = new(StringComparer.Ordinal)
    {
        "con", "prn", "aux", "nul",
        "com1", "com2", "com3", "com4", "com5", "com6", "com7", "com8", "com9",
        "lpt1", "lpt2", "lpt3", "lpt4", "lpt5", "lpt6", "lpt7", "lpt8", "lpt9",
    };

    /// <summary>Whether <paramref name="value"/> is a well-formed id.</summary>
    public static bool IsValid(string? value)
    {
        return value is { Length: >= 1 and <= MaxLength } && value.All(IsAllowed) && !_reserved.Contains(value);
    }

    /// <summary>Returns <paramref name="value"/> when it is a well-formed id.</summary>
    /// <exception cref="ArgumentException">The value is not a well-formed id.</exception>
    public static string Require(string? value)
    {
        return IsValid(value) ? value! : throw new ArgumentException(Description, nameof(value));
    }

    private static bool IsAllowed(char c) => c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '_' or '-';
}
