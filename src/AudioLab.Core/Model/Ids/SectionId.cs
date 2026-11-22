using System.Text.Json.Serialization;

namespace AudioLab.Core.Model;

/// <summary>Identifies an arrangement section.</summary>
[JsonConverter(typeof(IdJsonConverter<SectionId>))]
public readonly record struct SectionId : IAllocatedId<SectionId>
{
    private readonly string? _value;

    private SectionId(string value) => _value = value;

    /// <inheritdoc />
    public static string Prefix => "sec";

    /// <inheritdoc />
    public string Value => _value ?? string.Empty;

    /// <inheritdoc />
    public static SectionId From(string value) => new(IdRules.Require(value));

    /// <summary>The identifier text.</summary>
    public override string ToString() => Value;
}
