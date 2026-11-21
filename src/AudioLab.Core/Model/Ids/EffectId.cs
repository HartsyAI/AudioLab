using System.Text.Json.Serialization;

namespace AudioLab.Core.Model;

/// <summary>Identifies one effect in a track's insert chain.</summary>
/// <param name="Value">The identifier text.</param>
[JsonConverter(typeof(IdJsonConverter<EffectId>))]
public readonly record struct EffectId(string Value) : IAllocatedId<EffectId>
{
    /// <inheritdoc />
    public static string Prefix => "fxn";

    /// <inheritdoc />
    public static EffectId From(string value) => new(value);

    /// <summary>The identifier text.</summary>
    public override string ToString() => Value;
}
