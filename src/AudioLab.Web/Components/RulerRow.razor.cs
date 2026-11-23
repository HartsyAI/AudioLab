using System.Globalization;
using AudioLab.Core.Model;
using AudioLab.Web.Session;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

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

    /// <summary>Raised with the click when the row is pressed, which the timeline turns into a move of the playhead.</summary>
    [Parameter]
    public EventCallback<MouseEventArgs> OnSeek { get; set; }

    /// <summary>Maps a stored colour to the nearest track colour.</summary>
    [Parameter, EditorRequired]
    public TrackColorMap Colors { get; set; } = null!;

    private double Width => Geometry.Width(Length);

    // Only the bars row moves the playhead; a press on a section or a marker row is not a place in time.
    private Task SeekAsync(MouseEventArgs press) => Kind == RulerRowKind.Bars ? OnSeek.InvokeAsync(press) : Task.CompletedTask;

    private IEnumerable<double> Times => TimeTicks.Times(Geometry, Length);

    private double Left(double seconds) => Math.Round(Geometry.X(seconds));

    private string SectionStyle(Section section)
    {
        double left = Geometry.X(section.Start) + 1;
        double width = Math.Max(2, Geometry.Width(section.End - section.Start) - 2);
        return string.Create(CultureInfo.InvariantCulture, $"left: {left:0.##}px; width: {width:0.##}px; --block: {Colors.Token(section.Color)}");
    }

    private string MarkerStyle(Marker marker)
    {
        return string.Create(CultureInfo.InvariantCulture, $"left: {Geometry.X(marker.Time):0.##}px; --marker: {Colors.Token(marker.Color)}");
    }
}
