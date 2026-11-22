using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>One reason a command was refused, with the path of the value when there is one.</summary>
/// <param name="Code">The kind of refusal, for a client to branch on.</param>
/// <param name="Message">What is wrong, in one readable line of at most <see cref="ProjectLimits.MaxProblemLength"/> characters.</param>
public readonly record struct CommandError(CommandErrorCode Code, string Message);
