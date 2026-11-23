using System.Globalization;
using System.Text;
using AudioLab.Core.Model;
using AudioLab.Web.Session.Roll;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace AudioLab.Web.Components.PianoRoll;

/// <summary>The velocity of every note as a bar under its start: one path for the notes and one for the selection.</summary>
public sealed partial class VelocityLane : IDisposable
{
    private const string Label =
        "Velocity of each note; press a bar and drag to set it. Control with Up or Down raises or lowers the selected notes.";

    private MidiClip? _built;

    private int _version = -1;

    private double _scale = -1;

    private bool _rebuilt;

    private string _bars = string.Empty;

    private string _selected = string.Empty;

    private (VelocityDrag?, int) _drawn;

    private (RollGeometry, string) _shown;

    /// <summary>The clip.</summary>
    [Parameter, EditorRequired]
    public MidiClip Clip { get; set; } = null!;

    /// <summary>The scale.</summary>
    [Parameter]
    public RollGeometry Geometry { get; set; }

    /// <summary>The selected notes.</summary>
    [Parameter]
    public IReadOnlySet<NoteId> Selected { get; set; } = new HashSet<NoteId>();

    /// <summary>A number that changes when the selection does.</summary>
    [Parameter]
    public int Version { get; set; }

    /// <summary>The colour as a style value, already mapped to the nearest track token.</summary>
    [Parameter, EditorRequired]
    public string ColorToken { get; set; } = string.Empty;

    /// <summary>Raised with the press when the primary button goes down on the lane.</summary>
    [Parameter]
    public EventCallback<PointerEventArgs> OnPress { get; set; }

    [Inject]
    private RollDragState Drags { get; set; } = default!;

    private VelocityDrag? Drag => Drags.Latest<VelocityDrag>();

    private (VelocityDrag?, int) Shown => Drag is { } drag ? (drag, drag.Velocity) : (null, 0);

    /// <inheritdoc />
    public void Dispose()
    {
        Drags.Changed -= OnChanged;
    }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        Drags.Changed += OnChanged;
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        _rebuilt = false;
        bool sameClip = _built is not null && (ReferenceEquals(_built, Clip) || _built.Equals(Clip));
        double pixels = Geometry.PixelsPerSecond;
        if (!sameClip || _scale != pixels)
        {
            _bars = Bars(Clip.Notes.Select(note => (note.Start, note.Velocity)), pixels);
            _rebuilt = true;
        }
        if (!sameClip || _scale != pixels || _version != Version)
        {
            _selected = Bars(Clip.Notes.Where(note => Selected.Contains(note.Id)).Select(note => (note.Start, note.Velocity)), pixels);
            _rebuilt = true;
        }
        _built = Clip;
        _version = Version;
        _scale = pixels;
    }

    // A move of another gesture does not redraw the lane: it draws when its own bars, its own preview, its width or its colour change.
    /// <inheritdoc />
    protected override bool ShouldRender()
    {
        (VelocityDrag?, int) preview = Shown;
        bool draw = _rebuilt || preview != _drawn || _shown != (Geometry, ColorToken);
        _drawn = preview;
        _shown = (Geometry, ColorToken);
        return draw;
    }

    // One bar for each pixel column, the loudest of the notes that start in it: more would not show, and a big clip would be a huge path.
    private static string Bars(IEnumerable<(double Start, int Velocity)> notes, double pixelsPerSecond)
    {
        Dictionary<long, (double Start, int Velocity)> columns = [];
        foreach ((double start, int velocity) in notes)
        {
            long column = (long)Math.Floor(start * pixelsPerSecond);
            if (!columns.TryGetValue(column, out (double Start, int Velocity) held) || velocity > held.Velocity)
            {
                columns[column] = (start, velocity);
            }
        }
        StringBuilder path = new();
        foreach ((double start, int velocity) in columns.Values)
        {
            Bar(path, start, velocity);
        }
        return path.ToString();
    }

    private static void Bar(StringBuilder path, double start, int velocity)
    {
        path.Append('M').Append(start.ToString("0.####", CultureInfo.InvariantCulture)).Append(" 127V").Append(127 - velocity);
    }

    private string Preview(VelocityDrag drag)
    {
        return Bars(drag.Notes.Select(note => (note.Start, drag.Velocity)), Geometry.PixelsPerSecond);
    }

    private void OnChanged()
    {
        if (Shown != _drawn)
        {
            _ = InvokeAsync(StateHasChanged);
        }
    }
}
