using AudioLab.Core.Model;

namespace AudioLab.Core.Rules;

/// <summary>The track rules of the old DAW that are not about the mix: what a track may record and how tall its lane may be.</summary>
public static class TrackRules
{
    /// <summary>The lowest lane height, in pixels at the default zoom.</summary>
    public const double MinHeight = 16;

    /// <summary>The highest lane height, in pixels at the default zoom.</summary>
    public const double MaxHeight = 2000;

    /// <summary>The heights the track menu offers, by name.</summary>
    public static IReadOnlyList<(string Name, double Height)> HeightPresets { get; } =
        [("Tiny", 30), ("Compact", 44), ("Small", 64), ("Medium", 100), ("Large", 160)];

    /// <summary>Whether a track of this kind has an input to arm and monitor: an aux bus has none.</summary>
    public static bool HasInput(TrackKind kind) => kind != TrackKind.Bus;
}
