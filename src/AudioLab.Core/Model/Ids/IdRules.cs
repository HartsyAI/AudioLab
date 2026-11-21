namespace AudioLab.Core.Model;

/// <summary>What an id may look like: 1 to 64 ASCII letters, digits, underscores or hyphens, so it is safe in a path.</summary>
public static class IdRules
{
    /// <summary>The longest id, in characters.</summary>
    public const int MaxLength = 64;

    /// <summary>The rule in words, for an error message.</summary>
    public const string Description = "An id is 1 to 64 letters, digits, underscores or hyphens.";

    /// <summary>Whether <paramref name="value"/> is a well-formed id.</summary>
    public static bool IsValid(string? value)
    {
        return value is { Length: >= 1 and <= MaxLength } && value.All(IsAllowed);
    }

    /// <summary>Returns <paramref name="value"/> when it is a well-formed id.</summary>
    /// <exception cref="ArgumentException">The value is not a well-formed id.</exception>
    public static string Require(string? value)
    {
        return IsValid(value) ? value! : throw new ArgumentException(Description, nameof(value));
    }

    private static bool IsAllowed(char c)
    {
        return c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9') or '_' or '-';
    }
}
