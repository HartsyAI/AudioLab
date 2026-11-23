using System.Globalization;
using AudioLab.Core.Model;
using AudioLab.Core.Rules;
using AudioLab.Web.Interop;
using AudioLab.Web.Screens;
using AudioLab.Web.Services;
using AudioLab.Web.Session;
using AudioLab.Web.Session.Roll;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace AudioLab.Web.Components.PianoRoll;

/// <summary>The piano roll of one MIDI clip: keys, grid, notes, velocity lane and sustain pedal lane, edited by pointer and keyboard.</summary>
/// <remarks>
/// Every edit is commands sent in one gesture, with absolute targets (D23). A gesture follows the pointer through listeners on the document and is
/// previewed with the same rules the server applies; nothing is sent until the release. Notes are drawn as a few SVG paths and found by a pointer
/// in C#, so one handler serves the whole grid (D17, D55).
/// </remarks>
public sealed partial class RollEditor : IDisposable, IHandleEvent
{
    private const string ScrollId = "roll-scroll";

    private const string SurfaceId = "roll-surface";

    private const string VelocityId = "roll-velocity";

    private const string PedalId = "roll-pedal";

    private const double GripPixels = 6;

    private const double PickPixels = 6;

    private const int VelocityStep = 8;

    private const string NoSelection = "Select some notes first.";

    private readonly Dictionary<string, Func<Task>> _keys;

    private MidiClip? _built;

    private ProjectDocument? _document;

    private NoteIndex _index = null!;

    private RollLines _lines;

    private double _scale;

    private bool _overEdge;

    private bool _additive;

    private bool _centred;

    private int? _heldKey;

    private string _message = string.Empty;

    /// <summary>Makes the editor and the table of its keys.</summary>
    public RollEditor()
    {
        _keys = new Dictionary<string, Func<Task>>
        {
            [Shortcuts.RollSelectTool.Id] = () => UseTool(RollTool.Select),
            [Shortcuts.RollDrawTool.Id] = () => UseTool(RollTool.Draw),
            [Shortcuts.RollEraseTool.Id] = () => UseTool(RollTool.Erase),
            [Shortcuts.RollPrevious.Id] = () => Walk("ArrowLeft", extend: false),
            [Shortcuts.RollNext.Id] = () => Walk("ArrowRight", extend: false),
            [Shortcuts.RollAbove.Id] = () => Walk("ArrowUp", extend: false),
            [Shortcuts.RollBelow.Id] = () => Walk("ArrowDown", extend: false),
            [Shortcuts.RollExtendPrevious.Id] = () => Walk("ArrowLeft", extend: true),
            [Shortcuts.RollExtendNext.Id] = () => Walk("ArrowRight", extend: true),
            [Shortcuts.RollExtendAbove.Id] = () => Walk("ArrowUp", extend: true),
            [Shortcuts.RollExtendBelow.Id] = () => Walk("ArrowDown", extend: true),
            [Shortcuts.RollSelectAll.Id] = SelectAll,
            [Shortcuts.RollClear.Id] = ClearSelection,
            [Shortcuts.RollMoveEarlier.Id] = () => Nudge(-1),
            [Shortcuts.RollMoveLater.Id] = () => Nudge(1),
            [Shortcuts.RollTransposeUp.Id] = () => Transpose(1),
            [Shortcuts.RollTransposeDown.Id] = () => Transpose(-1),
            [Shortcuts.RollOctaveUp.Id] = () => Transpose(12),
            [Shortcuts.RollOctaveDown.Id] = () => Transpose(-12),
            [Shortcuts.RollLonger.Id] = () => Stretch(1),
            [Shortcuts.RollShorter.Id] = () => Stretch(-1),
            [Shortcuts.RollLouder.Id] = () => Louder(VelocityStep),
            [Shortcuts.RollSofter.Id] = () => Louder(-VelocityStep),
            [Shortcuts.RollInsertEarlier.Id] = () => MoveInsert(-1),
            [Shortcuts.RollInsertLater.Id] = () => MoveInsert(1),
            [Shortcuts.RollAddNote.Id] = TypeNote,
            [Shortcuts.RollDelete.Id] = Delete,
            [Shortcuts.RollDeleteAlternate.Id] = Delete,
            [Shortcuts.RollCopy.Id] = Copy,
            [Shortcuts.RollCut.Id] = Cut,
            [Shortcuts.RollPaste.Id] = Paste,
            [Shortcuts.RollDuplicate.Id] = Duplicate,
            [Shortcuts.RollQuantize.Id] = Quantize,
            [Shortcuts.RollPedal.Id] = TogglePedalAsync,
            [Shortcuts.RollAudition.Id] = ToggleAuditionKey,
            [Shortcuts.RollZoomIn.Id] = () => Zoom(zoomIn: true),
            [Shortcuts.RollZoomOut.Id] = () => Zoom(zoomIn: false),
        };
    }

    /// <summary>The document.</summary>
    [Parameter, EditorRequired]
    public ProjectDocument Document { get; set; } = null!;

    /// <summary>The track the clip is on, which gives the clip its colour.</summary>
    [Parameter, EditorRequired]
    public Track Track { get; set; } = null!;

    /// <summary>The clip.</summary>
    [Parameter, EditorRequired]
    public MidiClip Clip { get; set; } = null!;

    /// <summary>What the screen can do to the project, and why not when it cannot.</summary>
    [Parameter, EditorRequired]
    public SessionActions Actions { get; set; } = null!;

    [Inject]
    private PianoRollState State { get; set; } = default!;

    [Inject]
    private RollDragState Drags { get; set; } = default!;

    [Inject]
    private PianoRollKeys Keys { get; set; } = default!;

    [Inject]
    private NoteAudition Sound { get; set; } = default!;

    [Inject]
    private IMidiTransfer Transfer { get; set; } = default!;

    [Inject]
    private SessionSelection Selection { get; set; } = default!;

    [Inject]
    private IPointerTracker Pointer { get; set; } = default!;

    [Inject]
    private IElementView View { get; set; } = default!;

    [Inject]
    private TrackColorMap Colors { get; set; } = default!;

    [Inject]
    private ToastService Toasts { get; set; } = default!;

    private RollGeometry Geometry => new(State.PixelsPerSecond, RollGeometry.DefaultRowHeight, Clip.Length);

    private string ColorToken => Colors.Token(Clip.Color ?? Track.Color);

    private string ToolId => State.Tool.ToString().ToLowerInvariant();

    private string QuantizeName => State.Selected.Count == 0 ? "Quantize all notes" : "Quantize selected notes";

    private string NotesText => Clip.Notes.Count == 1 ? "1 note" : $"{Clip.Notes.Count.ToString(CultureInfo.InvariantCulture)} notes";

    private string LengthText
    {
        get
        {
            double bars = Math.Round(MidiClipPlan.Bars(Document, Clip), 2);
            return bars == 1 ? "1 bar" : $"{bars.ToString("0.##", CultureInfo.InvariantCulture)} bars";
        }
    }

    private string SurfaceLabel => $"Notes of {Clip.Name}. Arrow keys select notes, Alt with an arrow moves them, Enter adds a note.";

    private string Status
    {
        get
        {
            string selected = State.Selected.Count == 0
                ? "No notes selected."
                : $"{State.Selected.Count.ToString(CultureInfo.InvariantCulture)} of {Clip.Notes.Count.ToString(CultureInfo.InvariantCulture)}"
                    + " notes selected.";
            string note = State.Cursor is { } cursor && _index.Find(cursor) is { } found ? Describe(found) + " " : string.Empty;
            return $"{note}{selected} {_message}".Trim();
        }
    }

    private IReadOnlyList<SegmentItem> Tools =>
    [
        new SegmentItem(nameof(RollTool.Select).ToLowerInvariant(), "Select tool", Label: "Select", Shortcut: Shortcuts.RollSelectTool.Display),
        new SegmentItem(nameof(RollTool.Draw).ToLowerInvariant(), "Draw tool", Label: "Draw", Shortcut: Shortcuts.RollDrawTool.Display),
        new SegmentItem(nameof(RollTool.Erase).ToLowerInvariant(), "Erase tool", Label: "Erase", Shortcut: Shortcuts.RollEraseTool.Display),
    ];

    private IReadOnlyList<SegmentItem> GridItems
    {
        get
        {
            string? reason = Actions.CanEdit ? null : Actions.Reason;
            return
            [
                .. new (string Id, string Label)[]
                    {
                        ("auto", "Auto"), ("1/4", "1/4"), ("1/8", "1/8"), ("1/16", "1/16"), ("1/32", "1/32"), ("1/8t", "1/8t"),
                    }
                    .Select(item => new SegmentItem(item.Id, $"Grid {item.Label}", Label: item.Label, Reason: reason)),
            ];
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        State.Changed -= OnStateChanged;
        if (ReferenceEquals(Keys.Handler, _handler))
        {
            Keys.Handler = null;
        }
    }

    private Func<KeyChord, bool>? _handler;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        State.Changed += OnStateChanged;
        _handler = Handle;
        Keys.Handler = _handler;
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        bool newClip = _built is null || _built.Id != Clip.Id;
        if (newClip)
        {
            _centred = false;
        }
        if (_built is null || !(ReferenceEquals(_built, Clip) || _built.Equals(Clip)))
        {
            _index = NoteIndex.Of(Clip);
            _built = Clip;
        }
        if (!ReferenceEquals(_document, Document) || _scale != State.PixelsPerSecond || newClip)
        {
            _lines = RollGrid.Lines(Document, Clip, State.PixelsPerSecond);
            _document = Document;
            _scale = State.PixelsPerSecond;
        }
        State.Bind(Clip.Id);
        State.Prune(Clip);
    }

    /// <inheritdoc />
    protected override void OnAfterRender(bool firstRender)
    {
        if (_centred || !OperatingSystem.IsBrowser())
        {
            return;
        }
        _centred = true;
        // The clip opens with its notes in view: the roll is 128 rows tall and the panel shows a few.
        int middle = Clip.Notes.Count == 0 ? PianoRollState.DefaultPitch : (int)Math.Round(Clip.Notes.Average(note => note.Pitch));
        if (View.Bounds(ScrollId) is { } scroll)
        {
            View.ScrollTo(ScrollId, 0, Math.Max(0, Geometry.RowTop(middle) - (scroll.Height / 2)));
        }
    }

    private static string TransposeName(int semitones)
    {
        return semitones switch
        {
            12 => "Transpose up an octave",
            -12 => "Transpose down an octave",
            1 => "Transpose up a semitone",
            _ => "Transpose down a semitone",
        };
    }

    private static string Describe(MidiNote note)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"Note {NoteRules.Name(note.Pitch)}, starts at {note.Start:0.###} seconds, {note.Duration:0.###} long, velocity {note.Velocity}.");
    }

    /// <inheritdoc />
    /// <remarks>
    /// An event does not redraw the editor by itself: a pointer move over the grid would redraw it on every move. A change of state does, through
    /// <see cref="PianoRollState"/> and the revision of the document.
    /// </remarks>
    Task IHandleEvent.HandleEventAsync(EventCallbackWorkItem callback, object? arg) => callback.InvokeAsync(arg);

    private void OnStateChanged() => _ = InvokeAsync(StateHasChanged);

    private void ChooseTool(string id)
    {
        State.UseTool(Enum.Parse<RollTool>(id, ignoreCase: true));
    }

    private Task ChooseGrid(string grid) => Actions.Midi.SetGrid(grid);

    private Task StepLength(int bars) => Actions.Midi.StepLength(Clip.Id, bars);

    private List<MidiNote> SelectedNotes()
    {
        return [.. State.Selected.Select(id => _index.Find(id)).OfType<MidiNote>().OrderBy(note => note.Start).ThenBy(note => note.Pitch)];
    }

    // The notes a command acts on: the selection, or every note when none is selected, as the piano roll of the old DAW did.
    private List<MidiNote> SelectedOrAll() => State.Selected.Count == 0 ? [.. _index.Ordered] : SelectedNotes();

    private double SnapLocal(double seconds) => new GridSnap(Document).Snap(Clip.Start + seconds) - Clip.Start;

    // ----- pointer: notes

    private void NotesHover(PointerEventArgs move)
    {
        if (State.Tool != RollTool.Select || Drags.Current is not null || View.Bounds(SurfaceId) is not { } box)
        {
            return;
        }
        double seconds = Geometry.Seconds(move.ClientX - box.Left);
        double grip = GripPixels / State.PixelsPerSecond;
        bool over = _index.HitTest(seconds, Geometry.PitchAt(move.ClientY - box.Top), grip, out bool edge) is not null && edge;
        if (over != _overEdge)
        {
            _overEdge = over;
            StateHasChanged();
        }
    }

    private void NotesLeave()
    {
        _overEdge = false;
    }

    private void NotesPress(PointerEventArgs press)
    {
        if (press.Button != 0 || Drags.Current is not null || View.Bounds(SurfaceId) is not { } box)
        {
            return;
        }
        double x = press.ClientX - box.Left;
        double y = press.ClientY - box.Top;
        double seconds = Geometry.Seconds(x);
        _additive = press.ShiftKey || press.CtrlKey || press.MetaKey;
        MidiNote? hit = _index.HitTest(seconds, Geometry.PitchAt(y), GripPixels / State.PixelsPerSecond, out bool edge);
        if (State.Tool == RollTool.Erase)
        {
            if (Editable())
            {
                Begin(NoteDrag.ForErase(Document, Clip, Geometry, _index, x, y), SurfaceId);
            }
            return;
        }
        if (State.Tool == RollTool.Draw && hit is null)
        {
            if (Editable())
            {
                Begin(NoteDrag.ForDraw(Document, Clip, Geometry, _index, State.InsertVelocity, x, y), SurfaceId);
            }
            return;
        }
        if (hit is not { } note)
        {
            if (!_additive)
            {
                State.Clear();
            }
            State.MoveInsert(Math.Clamp(SnapLocal(seconds), 0, Clip.Length));
            Begin(NoteDrag.ForMarquee(Document, Clip, Geometry, _index, x, y), SurfaceId);
            return;
        }
        if (_additive)
        {
            State.Toggle(note.Id);
            return;
        }
        if (!State.Selected.Contains(note.Id))
        {
            State.Select([note.Id]);
        }
        State.Remember(note.Pitch, note.Velocity);
        Hear(note);
        if (!Editable())
        {
            return;
        }
        List<MidiNote> notes = SelectedNotes();
        Begin(
            edge
                ? NoteDrag.ForResize(Document, Clip, Geometry, _index, notes, note, x, y)
                : NoteDrag.ForMove(Document, Clip, Geometry, _index, notes, note, x, y),
            SurfaceId);
    }

    // ----- pointer: lanes

    private void VelocityPress(PointerEventArgs press)
    {
        if (press.Button != 0 || Drags.Current is not null || View.Bounds(VelocityId) is not { } box)
        {
            return;
        }
        double x = press.ClientX - box.Left;
        MidiNote? note = null;
        double best = PickPixels;
        foreach (MidiNote candidate in _index.Ordered)
        {
            double distance = Math.Abs(Geometry.X(candidate.Start) - x);
            if (distance < best || (distance == best && note is { } held && candidate.Velocity > held.Velocity))
            {
                best = distance;
                note = candidate;
            }
        }
        if (note is not { } target)
        {
            return;
        }
        if (!State.Selected.Contains(target.Id))
        {
            State.Select([target.Id]);
        }
        if (Editable())
        {
            Begin(new VelocityDrag(Clip.Id, SelectedNotes(), box.Height, press.ClientY - box.Top), VelocityId);
        }
    }

    private async Task PedalPressAsync(PointerEventArgs press)
    {
        if (press.Button != 0 || Drags.Current is not null || View.Bounds(PedalId) is not { } box)
        {
            return;
        }
        double x = press.ClientX - box.Left;
        MidiControlChange? change = null;
        double best = PickPixels;
        foreach (MidiControlChange candidate in Clip.ControlChanges)
        {
            double distance = Math.Abs(Geometry.X(candidate.Time) - x);
            if (candidate.Controller == PianoRollState.PedalController && distance < best)
            {
                best = distance;
                change = candidate;
            }
        }
        if (change is { } picked)
        {
            State.SelectPedal(picked.Time);
            if (Editable())
            {
                Begin(new PedalDrag(Document, Clip, Geometry, picked, x), PedalId);
            }
            return;
        }
        double time = Math.Clamp(SnapLocal(Geometry.Seconds(x)), 0, Clip.Length);
        State.SelectPedal(time);
        await SendAsync(RollEdits.TogglePedal(Clip, time));
    }

    // ----- gestures

    private void Begin(IRollGesture gesture, string originId)
    {
        Drags.Begin(gesture);
        Pointer.Begin(
            (x, y) =>
            {
                if (View.Bounds(originId) is { } box)
                {
                    gesture.Update(x - box.Left, y - box.Top);
                    Drags.Moved();
                }
            },
            (x, y) =>
            {
                if (View.Bounds(originId) is { } box)
                {
                    gesture.Update(x - box.Left, y - box.Top);
                }
                _ = InvokeAsync(() => ReleaseAsync(gesture));
            },
            () => Drags.End(gesture));
    }

    private async Task ReleaseAsync(IRollGesture gesture)
    {
        try
        {
            if (gesture is NoteDrag { Kind: NoteDragKind.Marquee } marquee)
            {
                if (marquee.Active)
                {
                    State.Select(marquee.Picked(), _additive);
                }
                return;
            }
            // The preview stays where it landed until the revision of the edit has been drawn.
            Drags.Moved();
            await SendAsync(gesture.Commit());
        }
        finally
        {
            Drags.End(gesture);
        }
    }

    // ----- sending

    private bool Editable()
    {
        if (Actions.CanEdit)
        {
            return true;
        }
        Toasts.Show(ToastKind.Warning, Actions.Reason ?? "The project cannot be edited now.");
        return false;
    }

    private async Task SendAsync(RollEdit? edit, string? refusal = null)
    {
        if (refusal is not null)
        {
            Toasts.Show(ToastKind.Warning, refusal);
        }
        if (edit is null || !Editable())
        {
            return;
        }
        EditResult result = await Actions.Midi.Send(edit.Build);
        if (result.Succeeded && edit.SelectCreated && result.Created.Count > 0)
        {
            State.Select(result.Created.Select(id => new NoteId(int.Parse(id, CultureInfo.InvariantCulture))));
        }
    }

    // ----- .mid files: the seam of IMidiTransfer, which has no implementation yet

    private async Task ImportFileAsync()
    {
        EditResult result = await Transfer.ImportAsync(Track.Id, MidiClipPlan.NextStart(Document, Track));
        if (!result.Succeeded && result.Refusal is { } reason)
        {
            Toasts.Show(ToastKind.Warning, reason);
        }
        else if (result.Created.Count > 0)
        {
            Selection.SelectClip(Track.Id, ClipId.From(result.Created[0]));
        }
    }

    private async Task ExportFileAsync()
    {
        EditResult result = await Transfer.ExportAsync(Clip.Id);
        if (!result.Succeeded && result.Refusal is { } reason)
        {
            Toasts.Show(ToastKind.Warning, reason);
        }
    }

    // ----- hearing notes

    private void ToggleAudition() => State.ToggleAudition();

    // A press on a key plays that pitch on the instrument of the track until the pointer is let go.
    private void KeyPress(PointerEventArgs press)
    {
        if (press.Button != 0 || !Sound.Available || View.Bounds("roll-keys") is not { } box)
        {
            return;
        }
        _heldKey = Geometry.PitchAt(press.ClientY - box.Top);
        _ = Sound.PressKeyAsync(Track.Id, _heldKey.Value, State.InsertVelocity);
    }

    private void KeyRelease(PointerEventArgs release)
    {
        if (_heldKey is { } pitch)
        {
            _heldKey = null;
            _ = Sound.ReleaseKeyAsync(Track.Id, pitch);
        }
    }

    // A note that is selected is heard for its own length when the user asked to hear notes.
    private void Hear(MidiNote note)
    {
        if (State.Audition && Sound.Available)
        {
            _ = Sound.HearNoteAsync(Track.Id, note);
        }
    }

    // ----- keyboard

    private bool Handle(KeyChord chord)
    {
        if (!View.HasFocusWithin(SurfaceId))
        {
            return false;
        }
        foreach (Shortcut shortcut in Shortcuts.Roll)
        {
            if (shortcut.Matches(chord) && _keys.TryGetValue(shortcut.Id, out Func<Task>? run))
            {
                _ = InvokeAsync(async () =>
                {
                    await run();
                    StateHasChanged();
                });
                return true;
            }
        }
        return false;
    }

    private Task ToggleAuditionKey()
    {
        if (Sound.Available)
        {
            State.ToggleAudition();
        }
        else
        {
            Toasts.Show(ToastKind.Warning, Sound.Unavailable ?? "Notes cannot be heard now.");
        }
        return Task.CompletedTask;
    }

    private Task UseTool(RollTool tool)
    {
        State.UseTool(tool);
        return Task.CompletedTask;
    }

    private Task Zoom(bool zoomIn)
    {
        State.Zoom(zoomIn);
        return Task.CompletedTask;
    }

    private Task SelectAll()
    {
        State.SelectAll(Clip);
        return Task.CompletedTask;
    }

    private Task ClearSelection()
    {
        State.Clear();
        return Task.CompletedTask;
    }

    private Task Walk(string key, bool extend)
    {
        if (_index.Walk(State.Cursor, key) is { } next && _index.Find(next) is { } note)
        {
            State.Select([next], additive: extend);
            State.Remember(note.Pitch, note.Velocity);
            Hear(note);
            State.MoveInsert(note.Start);
            Reveal(Geometry.X(note.Start), Geometry.RowTop(note.Pitch), Geometry.X(note.Duration));
        }
        return Task.CompletedTask;
    }

    private Task Nudge(int direction) => SendAsync(RollEdits.Nudge(Document, Clip, SelectedNotes(), direction));

    private Task Stretch(int direction) => SendAsync(RollEdits.Stretch(Document, Clip, SelectedNotes(), direction));

    private Task Louder(int amount) => SendAsync(RollEdits.Louder(Clip, SelectedOrAll(), amount));

    private Task Transpose(int semitones)
    {
        return Clip.Notes.Count == 0 ? Task.CompletedTask : SendAsync(RollEdits.Transpose(Clip, State.Selected, semitones));
    }

    private Task Quantize()
    {
        return Clip.Notes.Count == 0 ? Task.CompletedTask : SendAsync(RollEdits.Quantize(Clip, State.Selected));
    }

    private Task MoveInsert(int direction)
    {
        GridSnap grid = new(Document);
        double at = State.InsertTime;
        State.MoveInsert(Math.Clamp(grid.Snap(Clip.Start + at + (direction * grid.Step(Clip.Start + at))) - Clip.Start, 0, Clip.Length));
        return Task.CompletedTask;
    }

    private Task TypeNote()
    {
        RollEdit? edit = RollEdits.Type(Document, Clip, State.InsertTime, State.InsertPitch, State.InsertVelocity, out string? refusal);
        double step = new GridSnap(Document).Step(Clip.Start + State.InsertTime);
        State.MoveInsert(Math.Min(Clip.Length, State.InsertTime + step));
        return SendAsync(edit, refusal);
    }

    private Task Delete()
    {
        if (State.SelectedPedal is { } time)
        {
            return SendAsync(RollEdits.RemovePedal(Clip, time));
        }
        return State.Selected.Count == 0 ? Message(NoSelection) : SendAsync(RollEdits.Delete(Clip, State.Selected));
    }

    private Task Copy()
    {
        List<MidiNote> notes = SelectedNotes();
        if (notes.Count == 0)
        {
            return Message(NoSelection);
        }
        State.Copy(notes);
        return Message($"Copied {notes.Count.ToString(CultureInfo.InvariantCulture)} notes.");
    }

    private Task Cut()
    {
        List<MidiNote> notes = SelectedNotes();
        if (notes.Count == 0)
        {
            return Message(NoSelection);
        }
        State.Copy(notes);
        return SendAsync(RollEdits.Delete(Clip, State.Selected));
    }

    private Task Paste()
    {
        if (State.Clipboard.Count == 0)
        {
            return Message("There is nothing copied to paste.");
        }
        double span = State.Clipboard.Max(note => note.Start + (note.Duration ?? 0));
        RollEdit? edit = RollEdits.Place(Clip, State.Clipboard, State.InsertTime, out string? refusal);
        State.MoveInsert(Math.Min(Clip.Length, State.InsertTime + span));
        return SendAsync(edit, refusal);
    }

    private Task Duplicate()
    {
        List<MidiNote> notes = SelectedNotes();
        if (notes.Count == 0)
        {
            return Message(NoSelection);
        }
        RollEdit? edit = RollEdits.Duplicate(Clip, notes, out string? refusal);
        return SendAsync(edit, refusal);
    }

    private async Task TogglePedalAsync()
    {
        double time = Math.Clamp(State.InsertTime, 0, Clip.Length);
        State.SelectPedal(time);
        await SendAsync(RollEdits.TogglePedal(Clip, time));
    }

    private Task Message(string text)
    {
        _message = text;
        return Task.CompletedTask;
    }

    // Moves the view so a note is on screen: the keys at the left and the lanes at the bottom cover part of the scroll area.
    private void Reveal(double x, double y, double width)
    {
        if (!OperatingSystem.IsBrowser()
            || View.Bounds(ScrollId) is not { } scroll
            || View.Bounds(SurfaceId) is not { } surface
            || View.Bounds("roll-keys") is not { } keys
            || View.Bounds("roll-ruler") is not { } ruler
            || View.Bounds(VelocityId) is not { } lane
            || View.Scrolled(ScrollId) is not { } offset)
        {
            return;
        }
        const double Margin = 8;
        double left = surface.Left + x;
        double right = left + Math.Max(width, Margin);
        double top = surface.Top + y;
        double bottom = top + RollGeometry.DefaultRowHeight;
        double dx = 0;
        double dy = 0;
        if (left < keys.Left + keys.Width)
        {
            dx = left - (keys.Left + keys.Width) - Margin;
        }
        else if (right > scroll.Left + scroll.Width)
        {
            dx = right - (scroll.Left + scroll.Width) + Margin;
        }
        if (top < ruler.Top + ruler.Height)
        {
            dy = top - (ruler.Top + ruler.Height) - Margin;
        }
        else if (bottom > lane.Top)
        {
            dy = bottom - lane.Top + Margin;
        }
        if (dx != 0 || dy != 0)
        {
            View.ScrollTo(ScrollId, Math.Max(0, offset.Left + dx), Math.Max(0, offset.Top + dy));
        }
    }
}
