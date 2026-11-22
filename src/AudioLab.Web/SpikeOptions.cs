using System.Globalization;
using System.Web;

namespace AudioLab.Web;

/// <summary>What the spike page does, read from the query string so each measurement is one clean page load.</summary>
public sealed record SpikeOptions
{
    /// <summary>Width of one bar in pixels; 68 is the mock's zoom.</summary>
    public double BarWidth { get; init; } = 68;

    /// <summary>Number of tracks on the timeline.</summary>
    public int Tracks { get; init; } = 32;

    /// <summary>Number of clips on the timeline.</summary>
    public int Clips { get; init; } = 500;

    /// <summary>Length of the song in bars.</summary>
    public int Bars { get; init; } = 96;

    /// <summary>Where the engine runs: none, main or worker.</summary>
    public string Engine { get; init; } = "none";

    /// <summary>How long to play, measured in frames the audio thread played.</summary>
    public double Minutes { get; init; } = 10;

    /// <summary>The output buffering allowed, in milliseconds, including the context's own latency.</summary>
    public double BufferMs { get; init; } = 100;

    /// <summary>Frames in each chunk the engine renders.</summary>
    public int ChunkFrames { get; init; } = 512;

    /// <summary>Clip files fetched for the sample tracks.</summary>
    public int MediaClips { get; init; } = 8;

    /// <summary>Whether to drag and scroll the timeline all the while.</summary>
    public bool Load { get; init; }

    /// <summary>When to stall the producer once, in seconds of played audio; 0 for never.</summary>
    public double StallAtSeconds { get; init; }

    /// <summary>How long that stall lasts, in milliseconds.</summary>
    public int StallMilliseconds { get; init; } = 300;

    /// <summary>Label stored with every report, for example the build flavour.</summary>
    public string Label { get; init; } = string.Empty;

    /// <summary>Reads the options from a location's query string.</summary>
    public static SpikeOptions FromSearch(string search)
    {
        System.Collections.Specialized.NameValueCollection query = HttpUtility.ParseQueryString(search);
        return new SpikeOptions
        {
            BarWidth = Number(query["bar"], 68),
            Tracks = (int)Number(query["tracks"], 32),
            Clips = (int)Number(query["clips"], 500),
            Bars = (int)Number(query["bars"], 96),
            Engine = query["engine"] ?? "none",
            Minutes = Number(query["minutes"], 10),
            BufferMs = Number(query["buffer"], 100),
            ChunkFrames = (int)Number(query["chunk"], 512),
            MediaClips = (int)Number(query["media"], 8),
            Load = query["load"] == "1",
            StallAtSeconds = Number(query["stallat"], 0),
            StallMilliseconds = (int)Number(query["stallms"], 300),
            Label = query["label"] ?? string.Empty,
        };
    }

    private static double Number(string? text, double fallback)
    {
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) ? value : fallback;
    }
}
