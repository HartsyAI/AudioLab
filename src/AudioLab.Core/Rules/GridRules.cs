using AudioLab.Core.Model;

namespace AudioLab.Core.Rules;

/// <summary>The edit grid: what a grid value such as "1/8t" or "0.5s" means in seconds at a point of the timeline.</summary>
public static class GridRules
{
    /// <summary>The grid a project falls back to for "auto" or a value it does not know: a sixteenth note.</summary>
    public const double FallbackBeats = 0.25;

    /// <summary>The grid that follows the zoom of the view, which a server cannot know; it is read as <see cref="FallbackBeats"/>.</summary>
    public const string Auto = "auto";

    private static readonly Dictionary<string, double> _beats = new(StringComparer.Ordinal)
    {
        ["1/2"] = 2,
        ["1/4"] = 1,
        ["1/8"] = 0.5,
        ["1/16"] = 0.25,
        ["1/32"] = 0.125,
        ["1/8t"] = 1.0 / 3,
        ["1/16t"] = 1.0 / 6,
    };

    private static readonly Dictionary<string, double> _seconds = new(StringComparer.Ordinal)
    {
        ["1s"] = 1,
        ["0.5s"] = 0.5,
        ["0.1s"] = 0.1,
        ["10ms"] = 0.01,
    };

    /// <summary>The grid in quarter-note beats when it is musical, or null for a grid in seconds.</summary>
    public static double? Beats(string grid, TimeSignature signature)
    {
        ArgumentNullException.ThrowIfNull(grid);
        if (grid == "bar")
        {
            return MeterRules.BeatsPerBar(signature);
        }
        if (_seconds.ContainsKey(grid))
        {
            return null;
        }
        return _beats.TryGetValue(grid, out double beats) ? beats : FallbackBeats;
    }

    /// <summary>The length of one grid step, in seconds, at the tempo in force at <paramref name="at"/>.</summary>
    public static double StepSeconds(string grid, TempoMap tempo, TimeSignature signature, double at)
    {
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(tempo);
        return Beats(grid, signature) is { } beats ? beats * 60 / tempo.BpmAt(at) : _seconds[grid];
    }

    /// <summary>Every grid value a project may be set to, in the order the editor lists them: auto, a bar, note values, then times.</summary>
    public static IReadOnlyList<string> Values { get; } = [Auto, "bar", .. _beats.Keys, .. _seconds.Keys];

    /// <summary>Whether <paramref name="grid"/> is a grid value the editor offers.</summary>
    public static bool IsKnown(string grid) => grid == "bar" || _beats.ContainsKey(grid) || _seconds.ContainsKey(grid);
}
