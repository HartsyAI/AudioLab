using System.Text.Json.Serialization.Metadata;
using AudioLab.Core.Commands;
using ModelContextProtocol.Protocol;

namespace AudioLab.Server.Mcp;

/// <summary>The tools the server offers, built once at start: the read and history tools, and later one tool for each command.</summary>
public sealed class McpToolCatalog
{
    private readonly List<Tool> _tools = [];
    private readonly HashSet<string> _names = new(StringComparer.Ordinal);

    /// <summary>Builds the catalogue.</summary>
    public McpToolCatalog()
    {
        Add(Describe(
            McpToolNames.ListProjects,
            "Lists your projects, newest first, with the id each other tool takes.",
            McpJsonContext.Default.NoArguments,
            readOnly: true));
        Add(Describe(
            McpToolNames.GetProject,
            "Returns a project's document and its revision. A document longer than the server's limit is refused with a limit error.",
            McpJsonContext.Default.ProjectArguments,
            readOnly: true));
        Add(Describe(
            McpToolNames.GetHistory,
            "Returns the revision and what undo and redo hold: how many steps, and the command each would take back or put back.",
            McpJsonContext.Default.ProjectArguments,
            readOnly: true));
        Add(Describe(
            McpToolNames.Undo,
            "Goes back one undo step, for everyone who has the project open; refused when there is nothing to undo.",
            McpJsonContext.Default.ProjectArguments,
            readOnly: false));
        Add(Describe(
            McpToolNames.Redo,
            "Goes forward one redo step, for everyone who has the project open; refused when there is nothing to redo.",
            McpJsonContext.Default.ProjectArguments,
            readOnly: false));
    }

    /// <summary>Every tool, in the order a client lists them.</summary>
    public IReadOnlyList<Tool> Tools => _tools;

    /// <summary>Whether a tool of this name exists.</summary>
    public bool Contains(string name) => _names.Contains(name);

    private void Add(Tool tool)
    {
        if (!_names.Add(tool.Name))
        {
            throw new InvalidOperationException($"Two MCP tools are named {tool.Name}.");
        }
        _tools.Add(tool);
    }

    private static Tool Describe(string name, string description, JsonTypeInfo arguments, bool readOnly)
    {
        return new Tool
        {
            Name = name,
            Description = description,
            InputSchema = CommandSchema.For(arguments),
            Annotations = new ToolAnnotations { ReadOnlyHint = readOnly, OpenWorldHint = false },
        };
    }
}
