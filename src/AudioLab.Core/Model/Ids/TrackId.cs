using System.Text.Json.Serialization;

namespace AudioLab.Core.Model;

/// <summary>Identifies a track: audio, instrument or bus.</summary>
[JsonConverter(typeof(IdJsonConverter<TrackId>))]
public readonly record struct TrackId : IAllocatedId<TrackId>
{
    private TrackId(string value) => Value = value;

    /// <inheritdoc />
    public static string Prefix => "trk";

    /// <inheritdoc />
    public string Value { get; }

    /// <inheritdoc />
    public static TrackId From(string value) => new(IdRules.Require(value));

    /// <summary>The identifier text.</summary>
    public override string ToString() => Value ?? string.Empty;
}
