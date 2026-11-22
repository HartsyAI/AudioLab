namespace AudioLab.Core.Model;

/// <summary>The starting values shared by whatever creates sections, groups and tracks.</summary>
public static class ProjectDefaults
{
    /// <summary>The colour of a group that has none.</summary>
    public const string GroupColor = "#7a8cff";

    private static readonly string[] _sectionColors =
    [
        "#f4a261", "#f2c94c", "#b5d63d", "#5fcf80", "#2ec4b6", "#4cc9f0", "#4895ef", "#9d7bf0", "#d97ae0", "#ef6f91",
    ];

    private static readonly string[] _trackColors =
    [
        "#ee6d86", "#ef7d67", "#ee9a55", "#e3ad4f", "#a9cf5b", "#5fcf95",
        "#4fcdb8", "#4fbfdc", "#6c93f2", "#a487ee", "#cf7de0", "#e07ab5",
    ];

    /// <summary>The colour handed to the section at <paramref name="index"/>; the palette wraps.</summary>
    public static string SectionColor(int index) => _sectionColors[Wrap(index, _sectionColors.Length)];

    /// <summary>The colour of the track at <paramref name="index"/>: the design's twelve track tokens, in order.</summary>
    public static string TrackColor(int index) => _trackColors[Wrap(index, _trackColors.Length)];

    private static int Wrap(int index, int length) => ((index % length) + length) % length;
}
