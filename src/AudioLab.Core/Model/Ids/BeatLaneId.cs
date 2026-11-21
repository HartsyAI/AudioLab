using System.Text.Json.Serialization;

namespace AudioLab.Core.Model;

/// <summary>Identifies a lane of the beat pattern.</summary>
/// <param name="Value">The identifier text.</param>
[JsonConverter(typeof(IdJsonConverter<BeatLaneId>))]
public readonly record struct BeatLaneId(string Value) : IAllocatedId<BeatLaneId>
{
    /// <inheritdoc />
    public static string Prefix => "bln";

    /// <inheritdoc />
    public static BeatLaneId From(string value) => new(value);

    /// <summary>The identifier text.</summary>
    public override string ToString() => Value;
}
