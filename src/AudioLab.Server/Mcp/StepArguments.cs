using System.ComponentModel;
using System.Text.Json.Serialization;
using AudioLab.Core.Model;

namespace AudioLab.Server.Mcp;

/// <summary>The input of undo and redo: which project, and the revision the agent last read.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record StepArguments
{
    /// <summary>The id of one of your projects, as list_projects gives it.</summary>
    [Description("The id of one of your projects, as list_projects gives it.")]
    public required ProjectId Project { get; init; }

    /// <summary>The revision get_history reported; the step is taken only if the project is still at it.</summary>
    [Description("The revision get_history reported. The step is taken only if the project is still at that revision, so a change a person "
        + "made since is never undone by mistake.")]
    public required long ExpectedRevision { get; init; }
}
