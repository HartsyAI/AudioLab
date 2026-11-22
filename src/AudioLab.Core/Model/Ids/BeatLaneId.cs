using System.Text.Json.Serialization;

namespace AudioLab.Core.Model;

/// <summary>Identifies a lane of the beat pattern.</summary>
[JsonConverter(typeof(IdJsonConverter<BeatLaneId>))]
public readonly record struct BeatLaneId : IAllocatedId<BeatLaneId>
{
    private readonly string? _value;

    private BeatLaneId(string value) => _value = value;

    /// <inheritdoc />
    public static string Prefix => "bln";

    /// <inheritdoc />
    public string Value => _value ?? string.Empty;

    /// <inheritdoc />
    public static BeatLaneId From(string value) => new(IdRules.Require(value));

    /// <summary>The identifier text.</summary>
    public override string ToString() => Value;
}
