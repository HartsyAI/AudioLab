using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>The checks every handler makes before it edits, each answering with the refusal to return, or null when all is well.</summary>
internal static class CommandChecks
{
    /// <summary>A refusal naming the first number that is not finite; JSON cannot carry one, but a hand-built command can.</summary>
    public static CommandResult? Finite(params (string Name, double? Value)[] values)
    {
        foreach ((string name, double? value) in values)
        {
            if (value is { } number && !double.IsFinite(number))
            {
                return OutOfRange($"{name} must be a finite number.");
            }
        }
        return null;
    }

    /// <summary>A refusal when a given number is not from <paramref name="min"/> to <paramref name="max"/>, which a number that is not finite never is.</summary>
    public static CommandResult? Range(string name, double? value, double min, double max)
    {
        return value is { } number && !(number >= min && number <= max)
            ? OutOfRange($"{name} must be from {min.ToString(System.Globalization.CultureInfo.InvariantCulture)} to {max.ToString(System.Globalization.CultureInfo.InvariantCulture)}.")
            : null;
    }

    /// <summary>A refusal for a name that is empty, only white space, or longer than a project keeps, measured as sent.</summary>
    public static CommandResult? Named(string? name)
    {
        return string.IsNullOrWhiteSpace(name) ? Rejected("The name must not be blank.") : TooLong(name);
    }

    /// <summary>A refusal for an optional name longer than a project keeps, measured as sent; absent and blank names pass.</summary>
    public static CommandResult? TooLong(string? name)
    {
        return name is { Length: > ProjectLimits.MaxName }
            ? OutOfRange($"The name is at most {ProjectLimits.MaxName} characters.")
            : null;
    }

    /// <summary>A refusal for text longer than <paramref name="max"/> characters, measured as sent; absent text passes.</summary>
    public static CommandResult? Length(string name, string? value, int max)
    {
        return value is { } text && text.Length > max ? OutOfRange($"{name} is at most {max} characters.") : null;
    }

    /// <summary>The target of a command does not exist.</summary>
    public static CommandResult NotFound(string what, string id)
    {
        return CommandResult.Refuse(CommandErrorCode.NotFound, $"There is no {what} with the id {id}.");
    }

    /// <summary>A value is outside what the project allows, the same class of error as the validator's range failures.</summary>
    public static CommandResult OutOfRange(string message) => CommandResult.Refuse(CommandErrorCode.Invalid, message);

    /// <summary>The values are in range but a rule of the operation forbids the edit.</summary>
    public static CommandResult Rejected(string message) => CommandResult.Refuse(CommandErrorCode.Rejected, message);
}
