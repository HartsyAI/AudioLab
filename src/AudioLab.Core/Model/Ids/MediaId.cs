using System.Text.Json.Serialization;

namespace AudioLab.Core.Model;

/// <summary>Identifies one stored media file: a recording, an import, a generated clip or a sample.</summary>
/// <param name="Value">The identifier text.</param>
[JsonConverter(typeof(IdJsonConverter<MediaId>))]
public readonly record struct MediaId(string Value) : IAllocatedId<MediaId>
{
    /// <inheritdoc />
    public static string Prefix => "med";

    /// <inheritdoc />
    public static MediaId From(string value) => new(value);

    /// <summary>The identifier text.</summary>
    public override string ToString() => Value;
}
