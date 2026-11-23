using AudioLab.Core.Commands;
using AudioLab.Core.Model;

namespace AudioLab.Web.Session.Roll;

/// <summary>What the piano roll of this client holds that is not the document: tool, zoom, selection, insert point and clipboard.</summary>
/// <remarks>A view state, like <see cref="SessionSelection"/>: never in the project, never sent. It follows one clip.</remarks>
public sealed class PianoRollState
{
    /// <summary>The controller number of the sustain pedal.</summary>
    public const int PedalController = 64;

    /// <summary>The velocity a note that is drawn or typed takes when no other is asked for.</summary>
    public const int DefaultVelocity = 100;

    /// <summary>The pitch a note typed from the keyboard starts at, middle C.</summary>
    public const int DefaultPitch = 60;

    private readonly HashSet<NoteId> _selected = [];

    private ClipId? _clip;

    /// <summary>Raised when anything here changed.</summary>
    public event Action? Changed;

    /// <summary>The tool the pointer uses.</summary>
    public RollTool Tool { get; private set; } = RollTool.Select;

    /// <summary>How many pixels a second takes.</summary>
    public double PixelsPerSecond { get; private set; } = RollGeometry.DefaultPixelsPerSecond;

    /// <summary>A number that changes whenever the selection does, so a layer can tell without comparing sets.</summary>
    public int Version { get; private set; }

    /// <summary>The selected notes.</summary>
    public IReadOnlySet<NoteId> Selected => _selected;

    /// <summary>The note the keyboard walk is at, or null.</summary>
    public NoteId? Cursor { get; private set; }

    /// <summary>The time of the selected pedal event, or null.</summary>
    public double? SelectedPedal { get; private set; }

    /// <summary>Where a note typed from the keyboard, a paste or a pedal event goes, in seconds from the clip start.</summary>
    public double InsertTime { get; private set; }

    /// <summary>The pitch of the next note typed from the keyboard.</summary>
    public int InsertPitch { get; private set; } = DefaultPitch;

    /// <summary>The velocity of the next note that is drawn or typed.</summary>
    public int InsertVelocity { get; private set; } = DefaultVelocity;

    /// <summary>The notes that were copied, with their starts counted from the first.</summary>
    public IReadOnlyList<NewNote> Clipboard { get; private set; } = [];

    /// <summary>Points the state at a clip; another clip clears the selection and the insert point and keeps the tool, zoom and clipboard.</summary>
    public void Bind(ClipId? clip)
    {
        if (clip == _clip)
        {
            return;
        }
        _clip = clip;
        _selected.Clear();
        Cursor = null;
        SelectedPedal = null;
        InsertTime = 0;
        Version++;
        Changed?.Invoke();
    }

    /// <summary>Drops what the clip no longer holds.</summary>
    public void Prune(MidiClip clip)
    {
        ArgumentNullException.ThrowIfNull(clip);
        HashSet<NoteId> held = [.. clip.Notes.Select(note => note.Id)];
        bool changed = _selected.RemoveWhere(id => !held.Contains(id)) > 0;
        if (Cursor is { } cursor && !held.Contains(cursor))
        {
            Cursor = null;
            changed = true;
        }
        if (SelectedPedal is { } time
            && !clip.ControlChanges.Any(change => change.Controller == PedalController && Math.Abs(change.Time - time) < 1e-6))
        {
            SelectedPedal = null;
            changed = true;
        }
        if (InsertTime > clip.Length)
        {
            InsertTime = 0;
            changed = true;
        }
        if (changed)
        {
            Version++;
            Changed?.Invoke();
        }
    }

    /// <summary>Chooses a tool.</summary>
    public void UseTool(RollTool tool)
    {
        if (tool != Tool)
        {
            Tool = tool;
            Version++;
            Changed?.Invoke();
        }
    }

    /// <summary>Zooms in or out by one step.</summary>
    public void Zoom(bool zoomIn)
    {
        double next = RollGeometry.Zoomed(PixelsPerSecond, zoomIn);
        if (next != PixelsPerSecond)
        {
            PixelsPerSecond = next;
            Version++;
            Changed?.Invoke();
        }
    }

    /// <summary>Selects exactly these notes, or adds them to the selection; the last one has the cursor.</summary>
    public void Select(IEnumerable<NoteId> notes, bool additive = false)
    {
        ArgumentNullException.ThrowIfNull(notes);
        if (!additive)
        {
            _selected.Clear();
        }
        foreach (NoteId note in notes)
        {
            _selected.Add(note);
            Cursor = note;
        }
        SelectedPedal = null;
        Version++;
        Changed?.Invoke();
    }

    /// <summary>Adds a note to the selection, or takes it out when it is in.</summary>
    public void Toggle(NoteId note)
    {
        if (!_selected.Remove(note))
        {
            _selected.Add(note);
            Cursor = note;
        }
        SelectedPedal = null;
        Version++;
        Changed?.Invoke();
    }

    /// <summary>Selects every note of the clip.</summary>
    public void SelectAll(MidiClip clip)
    {
        ArgumentNullException.ThrowIfNull(clip);
        _selected.Clear();
        foreach (MidiNote note in clip.Notes)
        {
            _selected.Add(note.Id);
        }
        SelectedPedal = null;
        Version++;
        Changed?.Invoke();
    }

    /// <summary>Selects nothing.</summary>
    public void Clear()
    {
        if (_selected.Count == 0 && Cursor is null && SelectedPedal is null)
        {
            return;
        }
        _selected.Clear();
        Cursor = null;
        SelectedPedal = null;
        Version++;
        Changed?.Invoke();
    }

    /// <summary>Selects the pedal event at a time, and no note.</summary>
    public void SelectPedal(double? time)
    {
        _selected.Clear();
        SelectedPedal = time;
        Version++;
        Changed?.Invoke();
    }

    /// <summary>Moves the insert point.</summary>
    public void MoveInsert(double seconds)
    {
        InsertTime = Math.Max(0, seconds);
        Version++;
        Changed?.Invoke();
    }

    /// <summary>Remembers the pitch and velocity of the note that was last made or moved, so the next typed note follows it.</summary>
    public void Remember(int pitch, int velocity)
    {
        InsertPitch = Math.Clamp(pitch, 0, RollGeometry.Pitches - 1);
        InsertVelocity = Math.Clamp(velocity, 1, 127);
    }

    /// <summary>Keeps these notes for a paste.</summary>
    public void Copy(IEnumerable<MidiNote> notes)
    {
        ArgumentNullException.ThrowIfNull(notes);
        MidiNote[] copied = [.. notes.OrderBy(note => note.Start)];
        if (copied.Length == 0)
        {
            return;
        }
        double first = copied[0].Start;
        Clipboard = [.. copied.Select(note => new NewNote(note.Pitch, note.Start - first, note.Duration, note.Velocity))];
    }
}
