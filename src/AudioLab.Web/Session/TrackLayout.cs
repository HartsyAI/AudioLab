using AudioLab.Core.Model;

namespace AudioLab.Web.Session;

/// <summary>Where each track's row starts and how tall it is, from the document's heights; the headers and the lanes read the same rows.</summary>
/// <remarks>A row is its clip lane, then its automation lane when the track shows one (D69). The clip lane is <see cref="ClipHeights"/>.</remarks>
public sealed class TrackLayout
{
    /// <summary>The least a row is drawn, so a hand-edited file cannot make a header unusable.</summary>
    public const double MinimumHeight = 16;

    /// <summary>The height of an automation lane in pixels; the same value as <c>--automation-height</c> in tokens.css.</summary>
    public const double AutomationHeight = 40;

    private TrackLayout(double[] tops, double[] clipHeights, double[] heights, double total)
    {
        Tops = tops;
        ClipHeights = clipHeights;
        Heights = heights;
        Total = total;
    }

    /// <summary>The top of each track's row, in track order.</summary>
    public IReadOnlyList<double> Tops { get; }

    /// <summary>The height of each track's clip lane.</summary>
    public IReadOnlyList<double> ClipHeights { get; }

    /// <summary>The height of each whole row: the clip lane and the automation lane when it is shown.</summary>
    public IReadOnlyList<double> Heights { get; }

    /// <summary>The height of all the rows.</summary>
    public double Total { get; }

    /// <summary>The rows of a document's tracks.</summary>
    public static TrackLayout Of(ProjectDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        int count = document.Tracks.Count;
        double[] clipHeights = new double[count];
        double[] heights = new double[count];
        double[] tops = new double[count];
        double top = 0;
        for (int i = 0; i < count; i++)
        {
            TrackView view = document.Tracks[i].View;
            clipHeights[i] = Math.Max(MinimumHeight, view.Height);
            heights[i] = clipHeights[i] + (view.AutomationVisible ? AutomationHeight : 0);
            tops[i] = top;
            top += heights[i];
        }
        return new TrackLayout(tops, clipHeights, heights, top);
    }

    /// <summary>The top of a row's automation lane, in lane pixels; the row's own top when it shows none.</summary>
    public double AutomationTop(int row) => Tops[row] + ClipHeights[row];

    /// <summary>The row a point of the lanes falls in; a point below the last row is in the last row.</summary>
    public int RowAt(double y)
    {
        for (int row = 0; row < Tops.Count; row++)
        {
            if (y < Tops[row] + Heights[row])
            {
                return row;
            }
        }
        return Tops.Count - 1;
    }
}
