using System.Text.Json.Serialization;
using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>Why a command was refused.</summary>
[JsonConverter(typeof(StrictEnumConverter<CommandErrorCode>))]
public enum CommandErrorCode
{
    /// <summary>The command text is not valid JSON for its command, or holds a member it does not have.</summary>
    [JsonStringEnumMemberName("malformed")]
    Malformed,

    /// <summary>No command has that name, or none is registered for that type.</summary>
    [JsonStringEnumMemberName("unknown_command")]
    UnknownCommand,

    /// <summary>The command names another project than the one it was applied to.</summary>
    [JsonStringEnumMemberName("wrong_project")]
    WrongProject,

    /// <summary>The track, clip, marker or other target the command names does not exist.</summary>
    [JsonStringEnumMemberName("not_found")]
    NotFound,

    /// <summary>The command is well formed but a rule forbids it on this document, such as a section that would overlap.</summary>
    [JsonStringEnumMemberName("rejected")]
    Rejected,

    /// <summary>A limit of the server was reached, such as the most gestures a project may have open at once.</summary>
    [JsonStringEnumMemberName("limit")]
    Limit,

    /// <summary>The edit would leave a document that breaks the project rules; the message is the validator's.</summary>
    [JsonStringEnumMemberName("invalid")]
    Invalid,
}
