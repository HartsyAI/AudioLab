using System.Text.Json.Serialization;

namespace AudioLab.Core.Model;

/// <summary>Identifies one effect in a track's insert chain.</summary>
[JsonConverter(typeof(IdJsonConverter<EffectId>))]
public readonly record struct EffectId : IAllocatedId<EffectId>
{
    private readonly string? _value;

    private EffectId(string value) => _value = value;

    /// <inheritdoc />
    public static string Prefix => "fxn";

    /// <inheritdoc />
    public string Value => _value ?? string.Empty;

    /// <inheritdoc />
    public static EffectId From(string value) => new(IdRules.Require(value));

    /// <summary>The identifier text.</summary>
    public override string ToString() => Value;
}
