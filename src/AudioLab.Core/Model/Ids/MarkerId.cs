using System.Text.Json.Serialization;

namespace AudioLab.Core.Model;

/// <summary>Identifies a marker.</summary>
/// <param name="Value">The identifier text.</param>
[JsonConverter(typeof(IdJsonConverter<MarkerId>))]
public readonly record struct MarkerId(string Value) : IAllocatedId<MarkerId>
{
    /// <inheritdoc />
    public static string Prefix => "mrk";

    /// <inheritdoc />
    public static MarkerId From(string value) => new(value);

    /// <summary>The identifier text.</summary>
    public override string ToString() => Value;
}
