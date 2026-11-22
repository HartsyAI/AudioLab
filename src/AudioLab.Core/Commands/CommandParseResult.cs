using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>The outcome of reading a command from JSON: the command, or why the text is not one. Never both.</summary>
public sealed record CommandParseResult
{
    private CommandParseResult(Command? command, ValueList<CommandError> errors)
    {
        Command = command;
        Errors = errors;
    }

    /// <summary>The command; null when the text was refused.</summary>
    public Command? Command { get; }

    /// <summary>Why the text was refused; empty on success.</summary>
    public ValueList<CommandError> Errors { get; }

    /// <summary>Whether the text is a command.</summary>
    public bool Succeeded => Command is not null;

    internal static CommandParseResult Ok(Command command) => new(command, []);

    internal static CommandParseResult Refuse(CommandErrorCode code, string message)
    {
        return new CommandParseResult(null, [new CommandError(code, ProjectLimits.Shorten(message))]);
    }
}
