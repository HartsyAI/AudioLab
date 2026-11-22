using System.Text.Json.Serialization;

namespace AudioLab.Core.Model;

/// <summary>Identifies one stored media file: a recording, an import, a generated clip or a sample.</summary>
[JsonConverter(typeof(IdJsonConverter<MediaId>))]
public readonly record struct MediaId : IAllocatedId<MediaId>
{
    private readonly string? _value;

    private MediaId(string value) => _value = value;

    /// <inheritdoc />
    public static string Prefix => "med";

    /// <inheritdoc />
    public string Value => _value ?? string.Empty;

    /// <inheritdoc />
    public static MediaId From(string value) => new(IdRules.Require(value));

    /// <summary>The identifier text.</summary>
    public override string ToString() => Value;
}
