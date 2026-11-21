using System.Text.Json.Serialization;

namespace AudioLab.Core.Model;

/// <summary>Identifies one project.</summary>
[JsonConverter(typeof(IdJsonConverter<ProjectId>))]
public readonly record struct ProjectId : IAllocatedId<ProjectId>
{
    private ProjectId(string value) => Value = value;

    /// <inheritdoc />
    public static string Prefix => "prj";

    /// <inheritdoc />
    public string Value { get; }

    /// <inheritdoc />
    public static ProjectId From(string value) => new(IdRules.Require(value));

    /// <summary>The identifier text.</summary>
    public override string ToString() => Value ?? string.Empty;
}
