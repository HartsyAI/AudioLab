using System.Text.Json.Serialization;

namespace AudioLab.Core.Model;

/// <summary>Identifies one project.</summary>
/// <param name="Value">The identifier text.</param>
[JsonConverter(typeof(IdJsonConverter<ProjectId>))]
public readonly record struct ProjectId(string Value) : IAllocatedId<ProjectId>
{
    /// <inheritdoc />
    public static string Prefix => "prj";

    /// <inheritdoc />
    public static ProjectId From(string value) => new(value);

    /// <summary>The identifier text.</summary>
    public override string ToString() => Value;
}
