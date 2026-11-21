using System.Text.Json.Serialization;

namespace AudioLab.Core.Model;

/// <summary>Identifies a track: audio, instrument or bus.</summary>
/// <param name="Value">The identifier text.</param>
[JsonConverter(typeof(IdJsonConverter<TrackId>))]
public readonly record struct TrackId(string Value) : IAllocatedId<TrackId>
{
    /// <inheritdoc />
    public static string Prefix => "trk";

    /// <inheritdoc />
    public static TrackId From(string value) => new(value);

    /// <summary>The identifier text.</summary>
    public override string ToString() => Value;
}
