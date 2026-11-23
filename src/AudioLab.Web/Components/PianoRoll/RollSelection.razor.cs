using AudioLab.Core.Model;
using AudioLab.Web.Session.Roll;
using Microsoft.AspNetCore.Components;

namespace AudioLab.Web.Components.PianoRoll;

/// <summary>The selected notes drawn over the notes as one path, so a change of selection costs the notes it changes.</summary>
public sealed partial class RollSelection
{
    private const double MinPixels = 2;

    private MidiClip? _built;

    private int _version = -1;

    private double _scale;

    private bool _rebuilt;

    private string _path = string.Empty;

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

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        _rebuilt = false;
        if (_built is not null && _version == Version && _scale == Geometry.PixelsPerSecond && (ReferenceEquals(_built, Clip) || _built.Equals(Clip)))
        {
            return;
        }
        _rebuilt = true;
        _built = Clip;
        _version = Version;
        _scale = Geometry.PixelsPerSecond;
        _path = Selected.Count == 0
            ? string.Empty
            : RollNotes.Rectangles(Clip.Notes.Where(note => Selected.Contains(note.Id)), Clip.Length, MinPixels / Geometry.PixelsPerSecond);
    }

    /// <inheritdoc />
    protected override bool ShouldRender() => _rebuilt;
}
