using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>The outcome of a command: a new document, or the full list of reasons it was refused. Never both.</summary>
public sealed record CommandResult
{
    private CommandResult(ProjectDocument? document, ValueList<CommandError> errors, ValueList<string> created)
    {
        Document = document;
        Errors = errors;
        Created = created;
    }

    /// <summary>The document after the command; null when the command was refused.</summary>
    public ProjectDocument? Document { get; }

    /// <summary>Why the command was refused, at most <see cref="ProjectLimits.MaxProblems"/> entries then a line counting the rest; empty on success.</summary>
    public ValueList<CommandError> Errors { get; }

    /// <summary>The ids the handler allocated, in the order its command's description names them; empty when none.</summary>
    public ValueList<string> Created { get; }

    /// <summary>Whether the command was applied.</summary>
    public bool Succeeded => Document is not null;

    /// <summary>The command was applied and produced <paramref name="document"/>.</summary>
    /// <param name="document">The new document.</param>
    /// <param name="created">The ids the handler allocated, if any.</param>
    public static CommandResult Ok(ProjectDocument document, params ValueList<string> created)
    {
        ArgumentNullException.ThrowIfNull(document);
        return new CommandResult(document, [], created);
    }

    /// <summary>The command was refused for one reason.</summary>
    public static CommandResult Refuse(CommandErrorCode code, string message) => Refuse([new CommandError(code, message)]);

    /// <summary>A refusal built from the validator's list, which is already capped and ends with its own count line.</summary>
    internal static CommandResult RefuseInvalid(IReadOnlyList<string> problems)
    {
        return new CommandResult(null, new ValueList<CommandError>(problems.Select(problem => new CommandError(CommandErrorCode.Invalid, problem))), []);
    }

    /// <summary>The command was refused for these reasons; past the cap they are counted, not listed.</summary>
    /// <exception cref="ArgumentException">The list is empty: a refusal always says why.</exception>
    public static CommandResult Refuse(IReadOnlyList<CommandError> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        if (errors.Count == 0)
        {
            throw new ArgumentException("A refusal needs at least one error.", nameof(errors));
        }
        List<CommandError> kept = [];
        foreach (CommandError error in errors.Take(ProjectLimits.MaxProblems))
        {
            kept.Add(error with { Message = ProjectLimits.Shorten(error.Message) });
        }
        if (errors.Count > ProjectLimits.MaxProblems)
        {
            kept.Add(new CommandError(errors[ProjectLimits.MaxProblems].Code, $"and {errors.Count - ProjectLimits.MaxProblems} more."));
        }
        return new CommandResult(null, new ValueList<CommandError>(kept), []);
    }
}
