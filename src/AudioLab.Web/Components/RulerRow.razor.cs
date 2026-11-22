using System.Globalization;
using AudioLab.Core.Model;
using AudioLab.Web.Session;
using Microsoft.AspNetCore.Components;

namespace AudioLab.Web.Components;

/// <summary>The catalogue ruler row: sections, bars, time or markers, in label size, laid out by the one timeline scale.</summary>
public sealed partial class RulerRow
{
    /// <summary>Which row this is.</summary>
    [Parameter, EditorRequired]
    public RulerRowKind Kind { get; set; }

    /// <summary>The document the row reads its sections or markers from.</summary>
    [Parameter, EditorRequired]
    public ProjectDocument Document { get; set; } = null!;

    /// <summary>The scale from seconds to pixels.</summary>
    [Parameter]
    public TimelineGeometry Geometry { get; set; } = TimelineGeometry.Default;

    /// <summary>The bar lines, for the bars row.</summary>
    [Parameter]
    public IReadOnlyList<BarLine> Bars { get; set; } = [];

    /// <summary>How many seconds the lanes cover.</summary>
    [Parameter]
    public double Length { get; set; }

    /// <summary>Maps a stored colour to the nearest track colour.</summary>
    [Parameter, EditorRequired]
    public TrackColorMap Colors { get; set; } = null!;

    private double Width => Geometry.Width(Length);

    private IEnumerable<double> Times => TimeTicks.Times(Geometry, Length);

    private double Left(double seconds) => Math.Round(Geometry.X(seconds));

    private string SectionStyle(Section section)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"left: {Geometry.X(section.Start) + 1:0.##}px; width: {Math.Max(2, Geometry.Width(section.End - section.Start) - 2):0.##}px; --block: {Colors.Token(section.Color)}");
    }

    private string MarkerStyle(Marker marker)
    {
        return string.Create(CultureInfo.InvariantCulture, $"left: {Geometry.X(marker.Time):0.##}px; --marker: {Colors.Token(marker.Color)}");
    }
}
