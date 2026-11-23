using System.Globalization;
using AudioLab.Core.Model;
using AudioLab.Web.Session.Roll;
using Microsoft.AspNetCore.Components;

namespace AudioLab.Web.Components.PianoRoll;

/// <summary>What moves while the user works: the insert point, notes of a drag, an erase and a marquee; only this layer redraws on a move.</summary>
public sealed partial class RollOverlay : IDisposable
{
    private readonly Dictionary<NoteDrag, string> _carried = [];

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

    // The gestures that draw: those landing and the one the pointer is in, so a drop stays where it landed until its edit is answered.
    private List<NoteDrag> Shown
    {
        get
        {
            List<NoteDrag> shown = [.. Drags.Shown.OfType<NoteDrag>().Where(drag => drag.Active)];
            foreach (NoteDrag gone in _carried.Keys.Where(drag => !shown.Contains(drag)).ToList())
            {
                _carried.Remove(gone);
            }
            return shown;
        }
    }

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

    private string Rectangles(IEnumerable<MidiNote> notes) => RollNotes.Rectangles(notes, Clip.Length, Geometry.PixelsPerSecond);

    private void OnChanged() => _ = InvokeAsync(StateHasChanged);

    // A move draws the notes where they were once, and carries that drawing by a transform, so a pointer move changes one style and not a path
    // of every selected note; a resize and a draw change the notes themselves, which are few.
    private string Ghosts(NoteDrag drag)
    {
        if (drag.Kind != NoteDragKind.Move)
        {
            return Rectangles(drag.Ghosts);
        }
        if (!_carried.TryGetValue(drag, out string? path))
        {
            path = Rectangles(drag.Origins);
            _carried[drag] = path;
        }
        return path;
    }

    private string? Carried(NoteDrag drag)
    {
        return drag.Kind == NoteDragKind.Move
            ? string.Create(
                CultureInfo.InvariantCulture,
                $"transform: translate({Geometry.X(drag.Offset):0.##}px, {-drag.PitchOffset * RollGeometry.DefaultRowHeight:0.##}px)")
            : null;
    }

    private string Erased(NoteDrag drag) => Rectangles(drag.ErasedNotes);
}
