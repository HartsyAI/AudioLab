using System.Text.Json.Serialization.Metadata;
using AudioLab.Core.Commands;
using ModelContextProtocol.Protocol;

namespace AudioLab.Server.Mcp;

/// <summary>The tools the server offers, built once at start: the read and history tools, and one tool for each command of the registry.</summary>
public sealed class McpToolCatalog
{
    private readonly List<Tool> _tools = [];
    private readonly HashSet<string> _names = new(StringComparer.Ordinal);

    /// <summary>Builds the catalogue: a command's tool has the command's wire name, description and schema, as the registry gives them.</summary>
    /// <exception cref="InvalidOperationException">A command has the name of a tool that is not a command.</exception>
    public McpToolCatalog(CommandRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        Add(Describe(
            McpToolNames.ListProjects,
            "Lists your projects, newest first, with the id each other tool takes.",
            McpJsonContext.Default.NoArguments,
            readOnly: true));
        Add(Describe(
            McpToolNames.GetProject,
            "Reads a project. Without section it returns a summary: counts, and each track's id, name, kind and number of clips. "
            + "Read the rest in parts with section, track, clip, offset and count; a list says its total, so a long one is read in pages. "
            + "A result over the server's limit is refused with a limit error. The whole document is the resource audiolab://projects/{id}.",
            McpJsonContext.Default.GetProjectArguments,
            readOnly: true));
        Add(Describe(
            McpToolNames.GetHistory,
            "Returns the revision and what undo and redo hold: how many steps, and the command each would take back or put back. "
            + "Read it just before undo or redo, and pass its revision as expectedRevision.",
            McpJsonContext.Default.ProjectArguments,
            readOnly: true));
        Add(Describe(
            McpToolNames.Undo,
            UndoWarning("Goes back one undo step", "undo"),
            McpJsonContext.Default.StepArguments,
            readOnly: false,
            destructive: true));
        Add(Describe(
            McpToolNames.Redo,
            UndoWarning("Goes forward one redo step", "redo"),
            McpJsonContext.Default.StepArguments,
            readOnly: false,
            destructive: true));
        foreach (CommandDescriptor command in registry.Commands)
        {
            if (_names.Contains(command.Name))
            {
                throw new InvalidOperationException($"The command {command.Name} has the name of an MCP tool that is not a command.");
            }
            Add(new Tool
            {
                Name = command.Name,
                Description = McpToolHints.DescriptionOf(command),
                InputSchema = command.Schema,
                Annotations = McpToolHints.For(command),
            });
        }
    }

    /// <summary>Every tool, in the order a client lists them.</summary>
    public IReadOnlyList<Tool> Tools => _tools;

    /// <summary>Whether a tool of this name exists.</summary>
    public bool Contains(string name) => _names.Contains(name);

    private static string UndoWarning(string what, string step)
    {
        return $"{what}, for everyone who has the project open; refused when there is nothing to {step}. "
            + "The history is shared with the people using the project, so the step may be a change a person just made, not yours. "
            + $"Pass expectedRevision, the revision get_history reported: if the project has changed since, the call is refused as rejected "
            + $"with the current revision and nothing is changed. Every command you send is one undo step and clears the redo steps.";
    }

    private void Add(Tool tool)
    {
        if (!_names.Add(tool.Name))
        {
            throw new InvalidOperationException($"Two MCP tools are named {tool.Name}.");
        }
        _tools.Add(tool);
    }

    private static Tool Describe(string name, string description, JsonTypeInfo arguments, bool readOnly, bool destructive = false)
    {
        return new Tool
        {
            Name = name,
            Description = description,
            InputSchema = CommandSchema.For(arguments),
            Annotations = new ToolAnnotations
            {
                ReadOnlyHint = readOnly,
                DestructiveHint = destructive,
                IdempotentHint = readOnly,
                OpenWorldHint = false,
            },
        };
    }
}
