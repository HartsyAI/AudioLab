using AudioLab.Core.Commands;
using ModelContextProtocol.Protocol;

namespace AudioLab.Server.Mcp;

/// <summary>What a client is told about the effect of a command's tool, read from the command's wire name and its descriptor.</summary>
/// <remarks>
/// A wire name is <c>domain_verb</c> (D19). A command that removes, deletes, clears or ungroups is destructive; one that sets an absolute
/// value, renames, removes, deletes or clears is idempotent, since calling it again with the same arguments changes nothing more.
/// A command that adds, moves or transposes by an amount, splits or duplicates is neither. A hint is advice to a client, not a guarantee.
/// </remarks>
public static class McpToolHints
{
    /// <summary>Said in the description of a command that is no undo step.</summary>
    public const string NotUndoable = "It is not an undo step: undo does not take it back.";

    private static readonly string[] _destructive = ["remove", "delete", "clear", "ungroup"];

    private static readonly string[] _idempotentEnding = ["rename", "remove", "delete", "clear", "ungroup"];

    /// <summary>The annotations of the tool of <paramref name="command"/>.</summary>
    public static ToolAnnotations For(CommandDescriptor command)
    {
        ArgumentNullException.ThrowIfNull(command);
        string[] parts = command.Name.Split('_');
        return new ToolAnnotations
        {
            ReadOnlyHint = false,
            DestructiveHint = parts.Any(part => _destructive.Contains(part)),
            IdempotentHint = parts.Contains("set") || _idempotentEnding.Contains(parts[^1]),
            OpenWorldHint = false,
        };
    }

    /// <summary>The description of the tool of <paramref name="command"/>: the command's own, and that undo will not take it back when so.</summary>
    public static string DescriptionOf(CommandDescriptor command)
    {
        ArgumentNullException.ThrowIfNull(command);
        return command.Undoable ? command.Description : $"{command.Description} {NotUndoable}";
    }
}
