using System.Text.Json.Serialization;

namespace AudioLab.Core.Model;

/// <summary>The drum machine: lanes of steps looped over the project.</summary>
public sealed record BeatPattern
{
    /// <summary>How many steps one pass has.</summary>
    public required int Steps { get; init; }

    /// <summary>How far odd steps are pushed late, 0 to 1.</summary>
    public required double Swing { get; init; }

    /// <summary>The lanes, top to bottom.</summary>
    [JsonConverter(typeof(ValueListJsonConverter<BeatLane>))]
    public required ValueList<BeatLane> Lanes { get; init; }
}
