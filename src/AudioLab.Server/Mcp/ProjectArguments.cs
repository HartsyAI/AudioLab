using System.ComponentModel;
using System.Text.Json.Serialization;
using AudioLab.Core.Model;

namespace AudioLab.Server.Mcp;

/// <summary>The input of a tool that acts on one project.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ProjectArguments
{
    /// <summary>The id of one of your projects, as list_projects gives it.</summary>
    [Description("The id of one of your projects, as list_projects gives it.")]
    public required ProjectId Project { get; init; }
}
