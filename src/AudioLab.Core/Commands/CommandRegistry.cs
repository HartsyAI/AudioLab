using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>The one table of commands: wire name to handler, validated before any edit is accepted. Immutable and thread safe.</summary>
public sealed class CommandRegistry
{
    private readonly Dictionary<string, CommandRegistration> _byName = new(StringComparer.Ordinal);

    private readonly Dictionary<Type, CommandRegistration> _byType = [];

    internal CommandRegistry(IEnumerable<CommandRegistration> registrations)
    {
        List<CommandRegistration> ordered = [.. registrations.OrderBy(entry => entry.Descriptor.Name, StringComparer.Ordinal)];
        foreach (CommandRegistration entry in ordered)
        {
            _byName.Add(entry.Descriptor.Name, entry);
            _byType.Add(entry.Descriptor.CommandType, entry);
        }
        Commands = [.. ordered.Select(entry => entry.Descriptor)];
    }

    /// <summary>Every command by name, for a client or an MCP server to list and to generate tools from.</summary>
    public IReadOnlyList<CommandDescriptor> Commands { get; }

    /// <summary>The description of the command with this name, or null.</summary>
    public CommandDescriptor? Find(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return _byName.TryGetValue(name, out CommandRegistration? entry) ? entry.Descriptor : null;
    }

    /// <summary>The wire name of a command, or null when its type is not registered.</summary>
    public string? NameOf(Command command)
    {
        ArgumentNullException.ThrowIfNull(command);
        return _byType.TryGetValue(command.GetType(), out CommandRegistration? entry) ? entry.Descriptor.Name : null;
    }

    /// <summary>Reads the command called <paramref name="name"/> from JSON; any fault of the text is a refusal.</summary>
    public CommandParseResult Parse(string name, string json)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(json);
        if (_byName.TryGetValue(name, out CommandRegistration? entry))
        {
            return entry.Parse(json);
        }
        string named = CommandRegistryBuilder.IsValidName(name) ? $" named {name}" : string.Empty;
        return CommandParseResult.Refuse(CommandErrorCode.UnknownCommand, $"No command{named} exists.");
    }

    /// <summary>Applies a command: a new document that passed <see cref="ProjectValidator"/>, or every reason it was refused.</summary>
    /// <remarks>A refusal never changes anything. A handler's exception is a bug in the handler and is not caught.</remarks>
    /// <param name="document">The current document, which is not changed.</param>
    /// <param name="command">The command; a null member of a hand-built command is the caller's bug, not a refusal.</param>
    /// <param name="ids">The only source of ids a handler may use.</param>
    public CommandResult Execute(ProjectDocument document, Command command, IIdService ids)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(ids);
        if (!_byType.TryGetValue(command.GetType(), out CommandRegistration? entry))
        {
            return CommandResult.Refuse(CommandErrorCode.UnknownCommand, $"No command is registered for {command.GetType().Name}.");
        }
        if (command.Project != document.Id)
        {
            return CommandResult.Refuse(
                CommandErrorCode.WrongProject,
                $"The command is for project {command.Project.Value}, not {document.Id.Value}.");
        }
        CommandResult result = entry.Run(document, command, ids);
        return result.Document is { } edited ? Accept(document, edited, result) : result;
    }

    // A project keeps its identity through every edit, and what comes out of a handler is checked as a whole file is.
    private static CommandResult Accept(ProjectDocument before, ProjectDocument after, CommandResult result)
    {
        if (after.Id != before.Id || after.Owner != before.Owner || after.Version != before.Version)
        {
            return CommandResult.Refuse(CommandErrorCode.Invalid, "a command may not change the id, owner or version of a project.");
        }
        if (after == before)
        {
            return result;
        }
        IReadOnlyList<string> problems = ProjectValidator.Validate(after);
        return problems.Count == 0
            ? result
            : CommandResult.RefuseInvalid(problems);
    }
}
