using System.Text.Json.Serialization;

namespace AudioLab.Core.Model;

/// <summary>Identifies one stored media file: a recording, an import, a generated clip or a sample.</summary>
[JsonConverter(typeof(IdJsonConverter<MediaId>))]
public readonly record struct MediaId : IAllocatedId<MediaId>
{
    private MediaId(string value) => Value = value;

    /// <inheritdoc />
    public static string Prefix => "med";

    /// <inheritdoc />
    public string Value { get; }

    /// <inheritdoc />
    public static MediaId From(string value) => new(IdRules.Require(value));

    /// <summary>The identifier text.</summary>
    public override string ToString() => Value ?? string.Empty;
}
