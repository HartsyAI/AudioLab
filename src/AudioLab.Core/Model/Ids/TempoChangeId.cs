using System.Text.Json.Serialization;

namespace AudioLab.Core.Model;

/// <summary>Identifies a tempo change.</summary>
/// <param name="Value">The identifier text.</param>
[JsonConverter(typeof(IdJsonConverter<TempoChangeId>))]
public readonly record struct TempoChangeId(string Value) : IAllocatedId<TempoChangeId>
{
    /// <inheritdoc />
    public static string Prefix => "tmp";

    /// <inheritdoc />
    public static TempoChangeId From(string value) => new(value);

    /// <summary>The identifier text.</summary>
    public override string ToString() => Value;
}
