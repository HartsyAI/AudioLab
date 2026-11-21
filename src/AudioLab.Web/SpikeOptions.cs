using System.Globalization;
using System.Web;

namespace AudioLab.Web;

/// <summary>What the spike page shows, read from the query string so each measurement is one clean page load.</summary>
public sealed record SpikeOptions
{
    /// <summary>Which candidate draws the lanes.</summary>
    public RendererKind Renderer { get; init; } = RendererKind.Svg;

    /// <summary>Width of one bar in pixels; 68 is the mock's zoom.</summary>
    public double BarWidth { get; init; } = 68;

    /// <summary>Number of tracks.</summary>
    public int Tracks { get; init; } = 32;

    /// <summary>Number of clips over all tracks.</summary>
    public int Clips { get; init; } = 500;

    /// <summary>Length of the song in bars.</summary>
    public int Bars { get; init; } = 96;

    /// <summary>Whether to run the scripted scroll and drag scenarios and post the result.</summary>
    public bool Bench { get; init; }

    /// <summary>How many times each scenario runs.</summary>
    public int Runs { get; init; } = 3;

    /// <summary>Which scenarios the bench runs: all of them, only the two drags, or only picking a clip up and dropping it.</summary>
    public string Scenarios { get; init; } = "all";

    /// <summary>Label stored with the result, for example the build flavour.</summary>
    public string Label { get; init; } = string.Empty;

    /// <summary>Reads the options from a location's query string.</summary>
    public static SpikeOptions FromSearch(string search)
    {
        System.Collections.Specialized.NameValueCollection query = HttpUtility.ParseQueryString(search);
        return new SpikeOptions
        {
            Renderer = query["renderer"] switch
            {
                "svg-virtual" => RendererKind.SvgVirtual,
                "skia-gl" => RendererKind.SkiaGl,
                "skia-cpu" => RendererKind.SkiaCpu,
                "canvas2d" => RendererKind.Canvas2D,
                _ => RendererKind.Svg,
            },
            BarWidth = Number(query["bar"], 68),
            Tracks = (int)Number(query["tracks"], 32),
            Clips = (int)Number(query["clips"], 500),
            Bars = (int)Number(query["bars"], 96),
            Bench = query["bench"] == "1",
            Runs = (int)Number(query["runs"], 3),
            Label = query["label"] ?? string.Empty,
            Scenarios = query["scenarios"] ?? "all",
        };
    }

    private static double Number(string? text, double fallback)
    {
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) ? value : fallback;
    }
}
