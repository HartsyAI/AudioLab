using AudioLab.Core.Model;

namespace AudioLab.Web.Session;

/// <summary>Where each track's row starts and how tall it is, from the document's heights; the headers and the lanes read the same rows.</summary>
public sealed class TrackLayout
{
    /// <summary>The least a row is drawn, so a hand-edited file cannot make a header unusable.</summary>
    public const double MinimumHeight = 16;

    private TrackLayout(double[] tops, double[] heights, double total)
    {
        Tops = tops;
        Heights = heights;
        Total = total;
    }

    /// <summary>The top of each track's row, in track order.</summary>
    public IReadOnlyList<double> Tops { get; }

    /// <summary>The height of each row.</summary>
    public IReadOnlyList<double> Heights { get; }

    /// <summary>The height of all the rows.</summary>
    public double Total { get; }

    /// <summary>The rows of a document's tracks.</summary>
    public static TrackLayout Of(ProjectDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        double[] heights = [.. document.Tracks.Select(track => Math.Max(MinimumHeight, track.View.Height))];
        double[] tops = new double[heights.Length];
        double top = 0;
        for (int i = 0; i < heights.Length; i++)
        {
            tops[i] = top;
            top += heights[i];
        }
        return new TrackLayout(tops, heights, top);
    }
}
