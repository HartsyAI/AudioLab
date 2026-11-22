using AudioLab.Core.Model;

namespace AudioLab.Core.Rules;

/// <summary>The track rules of the old DAW that are not about the mix: what a track may record and how tall its lane may be.</summary>
public static class TrackRules
{
    /// <summary>The lowest lane height, in pixels at the default zoom.</summary>
    public const double MinHeight = 16;

    /// <summary>The highest lane height, in pixels at the default zoom.</summary>
    public const double MaxHeight = 2000;

    /// <summary>The longest input device id, in characters.</summary>
    public const int MaxDeviceIdLength = 512;

    /// <summary>The lowest input channel number of a single channel.</summary>
    public const int MinChannel = 1;

    /// <summary>The highest input channel number of a single channel.</summary>
    public const int MaxChannel = 256;

    /// <summary>The heights the track menu offers, by name.</summary>
    public static IReadOnlyList<(string Name, double Height)> HeightPresets { get; } =
        [("Tiny", 30), ("Compact", 44), ("Small", 64), ("Medium", 100), ("Large", 160)];

    /// <summary>Whether a track of this kind can be armed and monitored: an aux bus has no input to record.</summary>
    public static bool HasInput(TrackKind kind) => kind != TrackKind.Bus;

    /// <summary>Whether a track of this kind has an audio input to choose a device and channels for: only an audio track does.</summary>
    public static bool HasAudioInput(TrackKind kind) => kind == TrackKind.Audio;
}
