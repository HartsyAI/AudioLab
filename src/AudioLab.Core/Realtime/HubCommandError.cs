using System.Text.Json.Serialization;
using AudioLab.Core.Commands;

namespace AudioLab.Core.Realtime;

/// <summary>One reason a command was refused.</summary>
/// <param name="Code">The kind of refusal, for a client to branch on.</param>
/// <param name="Message">What is wrong, in one line.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record HubCommandError(CommandErrorCode Code, string Message);
