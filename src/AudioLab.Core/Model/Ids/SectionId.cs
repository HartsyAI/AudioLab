using System.Text.Json.Serialization;

namespace AudioLab.Core.Model;

/// <summary>Identifies an arrangement section.</summary>
/// <param name="Value">The identifier text.</param>
[JsonConverter(typeof(IdJsonConverter<SectionId>))]
public readonly record struct SectionId(string Value) : IAllocatedId<SectionId>
{
    /// <inheritdoc />
    public static string Prefix => "sec";

    /// <inheritdoc />
    public static SectionId From(string value) => new(value);

    /// <summary>The identifier text.</summary>
    public override string ToString() => Value;
}
