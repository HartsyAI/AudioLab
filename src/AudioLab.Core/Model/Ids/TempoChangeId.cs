using System.Text.Json.Serialization;

namespace AudioLab.Core.Model;

/// <summary>Identifies a tempo change.</summary>
[JsonConverter(typeof(IdJsonConverter<TempoChangeId>))]
public readonly record struct TempoChangeId : IAllocatedId<TempoChangeId>
{
    private readonly string? _value;

    private TempoChangeId(string value) => _value = value;

    /// <inheritdoc />
    public static string Prefix => "tmp";

    /// <inheritdoc />
    public string Value => _value ?? string.Empty;

    /// <inheritdoc />
    public static TempoChangeId From(string value) => new(IdRules.Require(value));

    /// <summary>The identifier text.</summary>
    public override string ToString() => Value;
}
