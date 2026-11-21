using System.Globalization;
using AudioLab.Web.Session;
using Microsoft.AspNetCore.Components;

namespace AudioLab.Web.Components;

/// <summary>One clip drawn as DOM with an inline SVG waveform; it redraws only when its own revision changes.</summary>
public sealed partial class SvgClip : ComponentBase
{
    private int _revision = -1;

    private bool _lifted;

    private double _left = double.NaN;

    private double _top = double.NaN;

    private double _bar;

    /// <summary>The clip.</summary>
    [Parameter]
    [EditorRequired]
    public ClipModel Clip { get; set; } = null!;

    /// <summary>The track the clip sits on, for its colour and mute state.</summary>
    [Parameter]
    [EditorRequired]
    public TrackModel Track { get; set; } = null!;

    /// <summary>Width of one bar in pixels.</summary>
    [Parameter]
    public double BarWidth { get; set; }

    /// <summary>Whether the clip is hidden because a drag has lifted it.</summary>
    [Parameter]
    public bool Hidden { get; set; }

    /// <summary>Whether the clip is the lifted copy that follows the pointer.</summary>
    [Parameter]
    public bool Lifted { get; set; }

    /// <summary>Left edge in lane pixels, overriding the clip's own position.</summary>
    [Parameter]
    public double? Left { get; set; }

    /// <summary>Top edge in lane pixels, overriding the clip's lane.</summary>
    [Parameter]
    public double? Top { get; set; }

    /// <summary>The revision of the clip this render shows; a changed value is what makes the clip redraw.</summary>
    [Parameter]
    public int Revision { get; set; }

    /// <summary>Lane height, to place the clip inside its lane.</summary>
    [Parameter]
    public double LaneHeight { get; set; }

    [Inject]
    private WavePathCache Paths { get; set; } = null!;

    private string CssClass => string.Join(' ', ["clip", Clip.Selected || Lifted ? "selected" : string.Empty, Lifted ? "lifted" : string.Empty, Track.Muted ? "muted" : string.Empty]);

    private string InlineStyle
    {
        get
        {
            double left = Left ?? ((Clip.StartBar * BarWidth) + 1);
            double width = (Clip.LengthBars * BarWidth) - 2;
            string position = Top is double top ? $"top: {Format(top)}px; bottom: auto; height: {Format(LaneHeight - 9)}px" : string.Empty;
            return $"--clip: var({Track.ColourToken}); left: {Format(left)}px; width: {Format(width)}px; {position}{(Hidden ? "; display: none" : string.Empty)}";
        }
    }

    /// <inheritdoc />
    protected override bool ShouldRender()
    {
        double left = Left ?? double.NaN;
        double top = Top ?? double.NaN;
        bool changed = Revision != _revision || Hidden != _lifted || BarWidth != _bar || !left.Equals(_left) || !top.Equals(_top);
        _revision = Revision;
        _lifted = Hidden;
        _bar = BarWidth;
        _left = left;
        _top = top;
        return changed;
    }

    private static string Format(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);
}
