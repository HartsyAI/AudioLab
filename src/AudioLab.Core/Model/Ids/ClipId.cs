using System.Text.Json.Serialization;

namespace AudioLab.Core.Model;

/// <summary>Identifies a clip, audio or MIDI. The two kinds share one id space.</summary>
/// <param name="Value">The identifier text.</param>
[JsonConverter(typeof(IdJsonConverter<ClipId>))]
public readonly record struct ClipId(string Value) : IAllocatedId<ClipId>
{
    /// <inheritdoc />
    public static string Prefix => "clp";

    /// <inheritdoc />
    public static ClipId From(string value) => new(value);

    /// <summary>The identifier text.</summary>
    public override string ToString() => Value;
}
