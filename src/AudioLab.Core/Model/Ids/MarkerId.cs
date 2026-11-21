using System.Text.Json.Serialization;

namespace AudioLab.Core.Model;

/// <summary>Identifies a marker.</summary>
[JsonConverter(typeof(IdJsonConverter<MarkerId>))]
public readonly record struct MarkerId : IAllocatedId<MarkerId>
{
    private MarkerId(string value) => Value = value;

    /// <inheritdoc />
    public static string Prefix => "mrk";

    /// <inheritdoc />
    public string Value { get; }

    /// <inheritdoc />
    public static MarkerId From(string value) => new(IdRules.Require(value));

    /// <summary>The identifier text.</summary>
    public override string ToString() => Value ?? string.Empty;
}
