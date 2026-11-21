using System.Text.Json.Serialization;

namespace AudioLab.Core.Model;

/// <summary>Identifies a clip, audio or MIDI. The two kinds share one id space.</summary>
[JsonConverter(typeof(IdJsonConverter<ClipId>))]
public readonly record struct ClipId : IAllocatedId<ClipId>
{
    private readonly string? _value;

    private ClipId(string value) => _value = value;

    /// <inheritdoc />
    public static string Prefix => "clp";

    /// <inheritdoc />
    public string Value => _value ?? string.Empty;

    /// <inheritdoc />
    public static ClipId From(string value) => new(IdRules.Require(value));

    /// <summary>The identifier text.</summary>
    public override string ToString() => Value;
}
