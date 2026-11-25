using System.Globalization;
using System.Text;
using AudioLab.Core.Model;
using AudioLab.Core.Rules;
using AudioLab.Web.Session;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace AudioLab.Web.Screens;

/// <summary>The lanes as DOM with inline SVG waveforms (D17): every clip is in the page and redraws only when its own record changes.</summary>
public sealed partial class LaneSurface
{
    private const double ClipGap = 2;

    // The grid svg is clipped to the surface, so its lines only need to be taller than any lane area.
    private const string LineHeight = "10000";

    /// <summary>The document.</summary>
    [Parameter, EditorRequired]
    public ProjectDocument Document { get; set; } = null!;

    /// <summary>The rows of the tracks.</summary>
    [Parameter, EditorRequired]
    public TrackLayout Layout { get; set; } = null!;

    /// <summary>The scale from seconds to pixels.</summary>
    [Parameter]
    public TimelineGeometry Geometry { get; set; } = TimelineGeometry.Default;

    /// <summary>The bar lines the grid is drawn at.</summary>
    [Parameter, EditorRequired]
    public IReadOnlyList<BarLine> Bars { get; set; } = [];

    /// <summary>How many seconds the lanes cover.</summary>
    [Parameter]
    public double Length { get; set; }

    /// <summary>The selected track, or null.</summary>
    [Parameter]
    public TrackId? SelectedTrack { get; set; }

    /// <summary>The selected clip, or null.</summary>
    [Parameter]
    public ClipId? SelectedClip { get; set; }

    /// <summary>The one clip that is a tab stop.</summary>
    [Parameter]
    public ClipId? TabStop { get; set; }

    /// <summary>Raised with a track when an empty part of its lane is pressed.</summary>
    [Parameter]
    public EventCallback<TrackId> OnSelectTrack { get; set; }

    /// <summary>Raised with a clip when it is pressed or activated.</summary>
    [Parameter]
    public EventCallback<ClipId> OnSelectClip { get; set; }

    /// <summary>Raised with a MIDI clip when it is double-clicked or opened with its key.</summary>
    [Parameter]
    public EventCallback<ClipId> OnOpenClip { get; set; }

    /// <summary>Raised with a track and the pixel from the start of the lane when an empty part of its lane is double-clicked.</summary>
    [Parameter]
    public EventCallback<(TrackId Track, double X)> OnLaneDoubleClick { get; set; }

    /// <summary>Raised for a key a clip hands up, such as an arrow.</summary>
    [Parameter]
    public EventCallback<(ClipId Clip, KeyboardEventArgs Key)> OnClipKey { get; set; }

    /// <summary>Raised with a clip and the press when the primary button goes down on it, which may begin a drag.</summary>
    [Parameter]
    public EventCallback<(ClipId Clip, PointerEventArgs Press)> OnClipPointerDown { get; set; }

    /// <summary>The automation lanes' view state: the selected point and the drag in progress.</summary>
    [Parameter]
    public AutomationLanes? Lanes { get; set; }

    /// <summary>Whether the automation lanes may be edited now.</summary>
    [Parameter]
    public bool CanEdit { get; set; }

    /// <summary>Why they may not, for the tooltip of a lane.</summary>
    [Parameter]
    public string? Reason { get; set; }

    /// <summary>Raised with a track and the pixel across and down its automation lane when an empty part of it is double-clicked.</summary>
    [Parameter]
    public EventCallback<(TrackId Track, double X, double Y)> OnAutomationAdd { get; set; }

    /// <summary>Raised with a track, the index of a point and the press when the point is pressed.</summary>
    [Parameter]
    public EventCallback<(TrackId Track, int Index, PointerEventArgs Press)> OnAutomationPointDown { get; set; }

    /// <summary>Raised with a track and a key an automation lane hands up.</summary>
    [Parameter]
    public EventCallback<(TrackId Track, KeyboardEventArgs Key)> OnAutomationKey { get; set; }

    [Inject]
    private TrackColorMap Colors { get; set; } = default!;

    private AutomationLanePlan PlanOf(Track track) => new(track.View.AutomationParameter, Geometry, TrackLayout.AutomationHeight);

    private IReadOnlyList<AutomationPoint> PointsOf(Track track) => AutomationRules.Lane(track.Automation, track.View.AutomationParameter);

    private IReadOnlyList<AutomationPoint>? PreviewOf(Track track) => Lanes?.Drag is { } drag && drag.Track == track.Id ? drag.Preview : null;

    private int? PreviewIndexOf(Track track) => Lanes?.Drag is { } drag && drag.Track == track.Id ? drag.PreviewIndex : null;

    private int? SelectedOf(Track track) => Lanes?.Track == track.Id ? Lanes.Index : null;

    private double Width => Math.Round(Geometry.Width(Length));

    private string StrongPath => GridPath(strong: true);

    private string WeakPath => GridPath(strong: false);

    private double AudioWidth(AudioClip clip) => Math.Max(ClipGap, Geometry.Width(ClipRules.VisibleLength(clip)) - ClipGap);

    private double MidiWidth(MidiClip clip) => Math.Max(ClipGap, Geometry.Width(clip.Length) - ClipGap);

    // One path of vertical lines for each strength, half a pixel in so a one-pixel line is crisp.
    private string GridPath(bool strong)
    {
        StringBuilder path = new();
        foreach (BarLine bar in Bars.Where(bar => bar.Strong == strong))
        {
            double x = Math.Round(Geometry.X(bar.Time)) + 0.5;
            path.Append('M').Append(x.ToString("0.#", CultureInfo.InvariantCulture)).Append(" 0V").Append(LineHeight);
        }
        return path.ToString();
    }
}
