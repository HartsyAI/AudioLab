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
                return Rejected($"{name} must be a finite number.");
            }
        }
        return null;
    }

    /// <summary>A refusal for a name that is empty or only white space.</summary>
    public static CommandResult? Named(string? name)
    {
        return string.IsNullOrWhiteSpace(name) ? Rejected("The name must not be blank.") : null;
    }

    /// <summary>The target of a command does not exist.</summary>
    public static CommandResult NotFound(string what, string id)
    {
        return CommandResult.Refuse(CommandErrorCode.NotFound, $"There is no {what} with the id {id}.");
    }

    /// <summary>A rule forbids the edit.</summary>
    public static CommandResult Rejected(string message) => CommandResult.Refuse(CommandErrorCode.Rejected, message);
}
