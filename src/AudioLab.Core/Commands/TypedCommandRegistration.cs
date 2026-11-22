using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>A command type with its strict JSON reader and its handler.</summary>
/// <typeparam name="TCommand">The command record.</typeparam>
internal sealed class TypedCommandRegistration<TCommand>(
    CommandDescriptor descriptor,
    JsonTypeInfo<TCommand> json,
    CommandHandler<TCommand> handler) : CommandRegistration(descriptor)
    where TCommand : Command
{
    /// <inheritdoc />
    public override CommandResult Run(ProjectDocument document, Command command, IIdService ids)
    {
        return handler(document, (TCommand)command, ids);
    }

    /// <inheritdoc />
    public override string Write(Command command)
    {
        return JsonSerializer.Serialize((TCommand)command, json);
    }

    /// <inheritdoc />
    public override CommandParseResult Parse(string text)
    {
        try
        {
            TCommand? command = JsonSerializer.Deserialize(text, json);
            return command is null
                ? CommandParseResult.Refuse(CommandErrorCode.Malformed, "The command is empty.")
                : CommandParseResult.Ok(command);
        }
        catch (JsonException exception)
        {
            return Malformed(exception);
        }
        catch (NotSupportedException exception)
        {
            return Malformed(JsonErrors.FromNotSupported(exception));
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return CommandParseResult.Refuse(CommandErrorCode.Malformed, JsonErrors.Clean(exception.Message));
        }
    }
}
