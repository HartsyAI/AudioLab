using System.Text.Json.Serialization;

namespace AudioLab.Core.Model;

/// <summary>Identifies a track group.</summary>
[JsonConverter(typeof(IdJsonConverter<GroupId>))]
public readonly record struct GroupId : IAllocatedId<GroupId>
{
    private readonly string? _value;

    private GroupId(string value) => _value = value;

    /// <inheritdoc />
    public static string Prefix => "grp";

    /// <inheritdoc />
    public string Value => _value ?? string.Empty;

    /// <inheritdoc />
    public static GroupId From(string value) => new(IdRules.Require(value));

    /// <summary>The identifier text.</summary>
    public override string ToString() => Value;
}
