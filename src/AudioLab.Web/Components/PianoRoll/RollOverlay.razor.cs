using System.Globalization;
using AudioLab.Core.Model;
using AudioLab.Web.Session.Roll;
using Microsoft.AspNetCore.Components;

namespace AudioLab.Web.Components.PianoRoll;

/// <summary>What moves while the user works: the insert point, notes of a drag, an erase and a marquee; only this layer redraws on a move.</summary>
public sealed partial class RollOverlay : IDisposable
{
    private NoteDrag? _carried;

    private string _carriedPath = string.Empty;

    /// <summary>The scale.</summary>
    [Parameter]
    public RollGeometry Geometry { get; set; }

    /// <summary>The clip, whose notes an erase highlights.</summary>
    [Parameter, EditorRequired]
    public MidiClip Clip { get; set; } = null!;

    /// <summary>The colour as a style value, already mapped to the nearest track token.</summary>
    [Parameter, EditorRequired]
    public string ColorToken { get; set; } = string.Empty;

    [Inject]
    private RollDragState Drags { get; set; } = default!;

    [Inject]
    private PianoRollState State { get; set; } = default!;

    private NoteDrag? Drag => Drags.Current as NoteDrag;

    /// <inheritdoc />
    public void Dispose()
    {
        Drags.Changed -= OnChanged;
        State.Changed -= OnChanged;
    }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        Drags.Changed += OnChanged;
        State.Changed += OnChanged;
    }

    private static string Rectangles(IEnumerable<MidiNote> notes, double clipLength, double least = 0.02)
    {
        return RollNotes.Rectangles(notes, clipLength, least);
    }

    private void OnChanged() => _ = InvokeAsync(StateHasChanged);

    // A move draws the notes where they were once, and carries that drawing by a transform, so a pointer move changes one style and not a path
    // of every selected note; a resize and a draw change the notes themselves, which are few.
    private string Ghosts(NoteDrag drag)
    {
        if (drag.Kind != NoteDragKind.Move)
        {
            return Rectangles(drag.Ghosts, Clip.Length);
        }
        if (!ReferenceEquals(_carried, drag))
        {
            _carried = drag;
            _carriedPath = Rectangles(drag.Origins, Clip.Length, 0.02);
        }
        return _carriedPath;
    }

    private string? Carried(NoteDrag drag)
    {
        return drag.Kind == NoteDragKind.Move
            ? string.Create(
                CultureInfo.InvariantCulture,
                $"transform: translate({Geometry.X(drag.Offset):0.##}px, {-drag.PitchOffset * RollGeometry.DefaultRowHeight:0.##}px)")
            : null;
    }

    private string Erased(NoteDrag drag) => Rectangles(Clip.Notes.Where(note => drag.Erased.Contains(note.Id)), Clip.Length);
}
