using System.Text.Json;

namespace AudioLab.Core.Commands;

/// <summary>What a client or an MCP server needs to know to offer one command: its name, its words and its input schema.</summary>
/// <param name="Name">The stable wire name: lower-case letters, digits and underscores, such as <c>track_rename</c>.</param>
/// <param name="Description">One sentence saying what the command does and which ids it allocates.</param>
/// <param name="CommandType">The command record.</param>
/// <param name="Schema">The JSON Schema of the command as sent, with the id and enum members spelled out.</param>
/// <param name="Undoable">False for a command that changes live state, such as arming a track, which is no undo step and is not taken back.</param>
public sealed record CommandDescriptor(string Name, string Description, Type CommandType, JsonElement Schema, bool Undoable = true);
