using System.Globalization;
using System.Text;
using AudioLab.Core.Model;
using AudioLab.Web.Session.Roll;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace AudioLab.Web.Components.PianoRoll;

/// <summary>The velocity of every note as a bar under its start: one path for the notes and one for the selection, so a drag of a bar redraws only a small preview.</summary>
public sealed partial class VelocityLane : IDisposable
{
    private MidiClip? _built;

    private int _version = -1;

    private double _length = -1;

    private string _bars = string.Empty;

    private string _selected = string.Empty;

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

    private VelocityDrag? Drag => Drags.Current as VelocityDrag;

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
        bool sameClip = _built is not null && (ReferenceEquals(_built, Clip) || _built.Equals(Clip));
        if (!sameClip || _length != Geometry.Length)
        {
            StringBuilder bars = new();
            foreach (MidiNote note in Clip.Notes)
            {
                Bar(bars, note.Start, note.Velocity);
            }
            _bars = bars.ToString();
        }
        if (!sameClip || _version != Version)
        {
            StringBuilder selected = new();
            foreach (MidiNote note in Clip.Notes.Where(note => Selected.Contains(note.Id)))
            {
                Bar(selected, note.Start, note.Velocity);
            }
            _selected = selected.ToString();
        }
        _built = Clip;
        _version = Version;
        _length = Geometry.Length;
    }

    private static void Bar(StringBuilder path, double start, int velocity)
    {
        path.Append('M').Append(start.ToString("0.####", CultureInfo.InvariantCulture)).Append(" 127V").Append(127 - velocity);
    }

    private static string Preview(VelocityDrag drag)
    {
        StringBuilder path = new();
        foreach (MidiNote note in drag.Notes)
        {
            Bar(path, note.Start, drag.Velocity);
        }
        return path.ToString();
    }

    private void OnChanged() => _ = InvokeAsync(StateHasChanged);
}
