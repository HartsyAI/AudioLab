using System.Text.Json.Serialization;

namespace AudioLab.Core.Model;

/// <summary>Identifies a lane of the beat pattern.</summary>
[JsonConverter(typeof(IdJsonConverter<BeatLaneId>))]
public readonly record struct BeatLaneId : IAllocatedId<BeatLaneId>
{
    private BeatLaneId(string value) => Value = value;

    /// <inheritdoc />
    public static string Prefix => "bln";

    /// <inheritdoc />
    public string Value { get; }

    /// <inheritdoc />
    public static BeatLaneId From(string value) => new(IdRules.Require(value));

    /// <summary>The identifier text.</summary>
    public override string ToString() => Value ?? string.Empty;
}
