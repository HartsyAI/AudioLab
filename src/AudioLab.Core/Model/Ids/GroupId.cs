using System.Text.Json.Serialization;

namespace AudioLab.Core.Model;

/// <summary>Identifies a track group.</summary>
/// <param name="Value">The identifier text.</param>
[JsonConverter(typeof(IdJsonConverter<GroupId>))]
public readonly record struct GroupId(string Value) : IAllocatedId<GroupId>
{
    /// <inheritdoc />
    public static string Prefix => "grp";

    /// <inheritdoc />
    public static GroupId From(string value) => new(value);

    /// <summary>The identifier text.</summary>
    public override string ToString() => Value;
}
