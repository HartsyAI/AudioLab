using System.Text.Json;
using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>One registered command type: how to read it, how to run it, and how to describe it.</summary>
internal abstract class CommandRegistration(CommandDescriptor descriptor)
{
    /// <summary>The public description of the command.</summary>
    public CommandDescriptor Descriptor { get; } = descriptor;

    /// <summary>Runs the handler on a command of exactly this registration's type.</summary>
    public abstract CommandResult Run(ProjectDocument document, Command command, IIdService ids);

    /// <summary>Reads the command from strict JSON; a bad text is a refusal, never an exception.</summary>
    public abstract CommandParseResult Parse(string json);

    /// <summary>Writes the command as the strict JSON that <see cref="Parse"/> reads.</summary>
    public abstract string Write(Command command);

    /// <summary>The error a bad command text gives, in one line that says where it happened.</summary>
    protected static CommandParseResult Malformed(JsonException exception)
    {
        return CommandParseResult.Refuse(CommandErrorCode.Malformed, JsonErrors.Describe(exception));
    }
}
