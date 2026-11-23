namespace AudioLab.Web.Services;

/// <summary>The one registry of shortcuts. A shortcut is never written into a component: menus, tooltips and key handlers read it here.</summary>
public static class Shortcuts
{
    /// <summary>The scope of the project list.</summary>
    public const string Projects = "projects";

    /// <summary>The scope of the session screen.</summary>
    public const string Session = "session";

    /// <summary>The scope of the piano roll: keys heard while the focus is inside it, so they never meet the keys of the arrangement.</summary>
    public const string PianoRoll = "pianoroll";

    /// <summary>The scope of the media library panel.</summary>
    public const string Media = "media";

    /// <summary>Rename the project that has the focus in the list.</summary>
    public static readonly Shortcut RenameProject = new("project.rename", "Rename project", "F2", Projects);

    /// <summary>Delete the project that has the focus in the list.</summary>
    public static readonly Shortcut DeleteProject = new("project.delete", "Delete project", "Delete", Projects);

    /// <summary>Go back one undo step.</summary>
    public static readonly Shortcut Undo = new("edit.undo", "Undo", "z", Session, Ctrl: true);

    /// <summary>Go forward one redo step.</summary>
    public static readonly Shortcut Redo = new("edit.redo", "Redo", "z", Session, Ctrl: true, Shift: true);

    /// <summary>Go forward one redo step, the way many editors also write it.</summary>
    public static readonly Shortcut RedoAlternate = new("edit.redo.alternate", "Redo", "y", Session, Ctrl: true);

    /// <summary>Rename the selected track.</summary>
    public static readonly Shortcut RenameTrack = new("track.rename", "Rename track", "F2", Session);

    /// <summary>Move the focused clip one grid step earlier.</summary>
    public static readonly Shortcut NudgeLeft = new("clip.nudge.left", "Move clip earlier", "ArrowLeft", Session, Alt: true);

    /// <summary>Move the focused clip one grid step later.</summary>
    public static readonly Shortcut NudgeRight = new("clip.nudge.right", "Move clip later", "ArrowRight", Session, Alt: true);

    /// <summary>Move the focused clip to the track above.</summary>
    public static readonly Shortcut NudgeUp = new("clip.nudge.up", "Move clip to the track above", "ArrowUp", Session, Alt: true);

    /// <summary>Move the focused clip to the track below.</summary>
    public static readonly Shortcut NudgeDown = new("clip.nudge.down", "Move clip to the track below", "ArrowDown", Session, Alt: true);

    /// <summary>Open the focused MIDI clip in the piano roll.</summary>
    public static readonly Shortcut OpenClip = new("clip.open", "Open in the piano roll", "Enter", Session);

    /// <summary>Add a MIDI clip to the selected instrument track.</summary>
    public static readonly Shortcut AddMidiClip = new("midi.clip.add", "Add MIDI clip", "n", Session);

    /// <summary>Lengthen the selected MIDI clip by one bar.</summary>
    public static readonly Shortcut MidiClipLonger = new("midi.clip.longer", "Lengthen MIDI clip by a bar", "]", Session);

    /// <summary>Shorten the selected MIDI clip by one bar.</summary>
    public static readonly Shortcut MidiClipShorter = new("midi.clip.shorter", "Shorten MIDI clip by a bar", "[", Session);

    /// <summary>Delete the media file that has the focus in the library.</summary>
    /// <remarks>Only while the focus is on a file of the library: the list's own key handler takes it, so it never reaches the transport.</remarks>
    public static readonly Shortcut DeleteMedia = new("media.delete", "Delete media file", "Delete", Media);

    /// <summary>Add the media file that has the focus in the library to the arrangement.</summary>
    /// <remarks>Only while the focus is on a file of the library: the list's own key handler takes it, so it never reaches the transport.</remarks>
    public static readonly Shortcut AddMedia = new("media.add", "Add to the arrangement", "Enter", Media);

    /// <summary>Move the focus to the next file of the library.</summary>
    /// <remarks>Only while the focus is on a file of the library: the list's own key handler takes it, so it never reaches the transport.</remarks>
    public static readonly Shortcut MediaNext = new("media.next", "Next file", "ArrowDown", Media);

    /// <summary>Move the focus to the previous file of the library.</summary>
    /// <remarks>Only while the focus is on a file of the library: the list's own key handler takes it, so it never reaches the transport.</remarks>
    public static readonly Shortcut MediaPrevious = new("media.previous", "Previous file", "ArrowUp", Media);

    /// <summary>Move the focus to the first file of the library.</summary>
    /// <remarks>Only while the focus is on a file of the library: the list's own key handler takes it, so it never reaches the transport.</remarks>
    public static readonly Shortcut MediaFirst = new("media.first", "First file", "Home", Media);

    /// <summary>Move the focus to the last file of the library.</summary>
    /// <remarks>Only while the focus is on a file of the library: the list's own key handler takes it, so it never reaches the transport.</remarks>
    public static readonly Shortcut MediaLast = new("media.last", "Last file", "End", Media);

    /// <summary>Play from the playhead, or pause while playing.</summary>
    public static readonly Shortcut PlayPause = new("transport.play", "Play or pause", " ", Session);

    /// <summary>Stop and return to where playing began.</summary>
    public static readonly Shortcut Stop = new("transport.stop", "Stop", " ", Session, Shift: true);

    /// <summary>Move the playhead to the start.</summary>
    public static readonly Shortcut ToStart = new("transport.start", "Go to start", "Home", Session);

    /// <summary>Move the playhead to the end of what is heard.</summary>
    public static readonly Shortcut ToEnd = new("transport.end", "Go to end", "End", Session);

    /// <summary>Move the playhead one bar earlier.</summary>
    public static readonly Shortcut SeekBack = new("transport.back", "Back one bar", "ArrowLeft", Session, Shift: true);

    /// <summary>Move the playhead one bar later.</summary>
    public static readonly Shortcut SeekForward = new("transport.forward", "Forward one bar", "ArrowRight", Session, Shift: true);

    /// <summary>Turn the loop on or off.</summary>
    public static readonly Shortcut ToggleLoop = new("transport.loop", "Loop", "l", Session);

    /// <summary>Turn the metronome on or off.</summary>
    public static readonly Shortcut ToggleClick = new("transport.click", "Click", "k", Session);

    /// <summary>Use the select tool in the piano roll.</summary>
    public static readonly Shortcut RollSelectTool = new("roll.tool.select", "Select tool", "v", PianoRoll);

    /// <summary>Use the draw tool in the piano roll.</summary>
    public static readonly Shortcut RollDrawTool = new("roll.tool.draw", "Draw tool", "d", PianoRoll);

    /// <summary>Use the erase tool in the piano roll.</summary>
    public static readonly Shortcut RollEraseTool = new("roll.tool.erase", "Erase tool", "e", PianoRoll);

    /// <summary>Select the previous note in time.</summary>
    public static readonly Shortcut RollPrevious = new("roll.walk.previous", "Select previous note", "ArrowLeft", PianoRoll);

    /// <summary>Select the next note in time.</summary>
    public static readonly Shortcut RollNext = new("roll.walk.next", "Select next note", "ArrowRight", PianoRoll);

    /// <summary>Select the nearest note above.</summary>
    public static readonly Shortcut RollAbove = new("roll.walk.above", "Select note above", "ArrowUp", PianoRoll);

    /// <summary>Select the nearest note below.</summary>
    public static readonly Shortcut RollBelow = new("roll.walk.below", "Select note below", "ArrowDown", PianoRoll);

    /// <summary>Add the previous note in time to the selection.</summary>
    public static readonly Shortcut RollExtendPrevious =
        new("roll.extend.previous", "Add previous note to selection", "ArrowLeft", PianoRoll, Shift: true);

    /// <summary>Add the next note in time to the selection.</summary>
    public static readonly Shortcut RollExtendNext = new("roll.extend.next", "Add next note to selection", "ArrowRight", PianoRoll, Shift: true);

    /// <summary>Add the nearest note above to the selection.</summary>
    public static readonly Shortcut RollExtendAbove = new("roll.extend.above", "Add note above to selection", "ArrowUp", PianoRoll, Shift: true);

    /// <summary>Add the nearest note below to the selection.</summary>
    public static readonly Shortcut RollExtendBelow = new("roll.extend.below", "Add note below to selection", "ArrowDown", PianoRoll, Shift: true);

    /// <summary>Select every note.</summary>
    public static readonly Shortcut RollSelectAll = new("roll.select.all", "Select all notes", "a", PianoRoll, Ctrl: true);

    /// <summary>Select nothing.</summary>
    public static readonly Shortcut RollClear = new("roll.select.none", "Clear selection", "Escape", PianoRoll);

    /// <summary>Move the selected notes one grid step earlier.</summary>
    public static readonly Shortcut RollMoveEarlier = new("roll.move.earlier", "Move notes one step earlier", "ArrowLeft", PianoRoll, Alt: true);

    /// <summary>Move the selected notes one grid step later.</summary>
    public static readonly Shortcut RollMoveLater = new("roll.move.later", "Move notes one step later", "ArrowRight", PianoRoll, Alt: true);

    /// <summary>Transpose the selected notes up a semitone.</summary>
    public static readonly Shortcut RollTransposeUp = new("roll.transpose.up", "Transpose up a semitone", "ArrowUp", PianoRoll, Alt: true);

    /// <summary>Transpose the selected notes down a semitone.</summary>
    public static readonly Shortcut RollTransposeDown = new("roll.transpose.down", "Transpose down a semitone", "ArrowDown", PianoRoll, Alt: true);

    /// <summary>Transpose the selected notes up an octave.</summary>
    public static readonly Shortcut RollOctaveUp = new("roll.octave.up", "Transpose up an octave", "PageUp", PianoRoll, Alt: true);

    /// <summary>Transpose the selected notes down an octave.</summary>
    public static readonly Shortcut RollOctaveDown =
        new("roll.octave.down", "Transpose down an octave", "PageDown", PianoRoll, Alt: true);

    /// <summary>Lengthen the selected notes by one grid step.</summary>
    public static readonly Shortcut RollLonger =
        new("roll.length.longer", "Lengthen notes by a step", "]", PianoRoll, Alt: true);

    /// <summary>Shorten the selected notes by one grid step.</summary>
    public static readonly Shortcut RollShorter =
        new("roll.length.shorter", "Shorten notes by a step", "[", PianoRoll, Alt: true);

    /// <summary>Raise the velocity of the selected notes.</summary>
    public static readonly Shortcut RollLouder = new("roll.velocity.up", "Raise velocity", "ArrowUp", PianoRoll, Ctrl: true);

    /// <summary>Lower the velocity of the selected notes.</summary>
    public static readonly Shortcut RollSofter = new("roll.velocity.down", "Lower velocity", "ArrowDown", PianoRoll, Ctrl: true);

    /// <summary>Move the insert point one grid step earlier.</summary>
    public static readonly Shortcut RollInsertEarlier = new("roll.insert.earlier", "Move insert point earlier", "ArrowLeft", PianoRoll, Ctrl: true);

    /// <summary>Move the insert point one grid step later.</summary>
    public static readonly Shortcut RollInsertLater = new("roll.insert.later", "Move insert point later", "ArrowRight", PianoRoll, Ctrl: true);

    /// <summary>Type a note at the insert point.</summary>
    public static readonly Shortcut RollAddNote = new("roll.note.add", "Add note at insert point", "Enter", PianoRoll);

    /// <summary>Delete the selected notes or pedal event.</summary>
    public static readonly Shortcut RollDelete = new("roll.delete", "Delete", "Delete", PianoRoll);

    /// <summary>Delete the selected notes or pedal event, as many editors also write it.</summary>
    public static readonly Shortcut RollDeleteAlternate = new("roll.delete.alternate", "Delete", "Backspace", PianoRoll);

    /// <summary>Copy the selected notes.</summary>
    public static readonly Shortcut RollCopy = new("roll.copy", "Copy notes", "c", PianoRoll, Ctrl: true);

    /// <summary>Cut the selected notes.</summary>
    public static readonly Shortcut RollCut = new("roll.cut", "Cut notes", "x", PianoRoll, Ctrl: true);

    /// <summary>Paste the copied notes at the insert point.</summary>
    public static readonly Shortcut RollPaste = new("roll.paste", "Paste notes", "v", PianoRoll, Ctrl: true);

    /// <summary>Copy the selected notes right after themselves.</summary>
    public static readonly Shortcut RollDuplicate = new("roll.duplicate", "Duplicate notes", "d", PianoRoll, Ctrl: true);

    /// <summary>Snap the selected notes, or all of them, to the grid.</summary>
    public static readonly Shortcut RollQuantize = new("roll.quantize", "Quantize notes", "q", PianoRoll);

    /// <summary>Put the sustain pedal down or up at the insert point.</summary>
    public static readonly Shortcut RollPedal = new("roll.pedal", "Toggle sustain pedal at insert point", "p", PianoRoll);

    /// <summary>Hear a note when it is selected, or stop doing so.</summary>
    public static readonly Shortcut RollAudition = new("roll.audition", "Hear notes as they are selected", "h", PianoRoll);

    /// <summary>Zoom the time axis in.</summary>
    public static readonly Shortcut RollZoomIn = new("roll.zoom.in", "Zoom in", "=", PianoRoll);

    /// <summary>Zoom the time axis out.</summary>
    public static readonly Shortcut RollZoomOut = new("roll.zoom.out", "Zoom out", "-", PianoRoll);

    /// <summary>Every shortcut of the piano roll, in the order they are listed.</summary>
    public static readonly IReadOnlyList<Shortcut> Roll =
    [
        RollSelectTool, RollDrawTool, RollEraseTool, RollPrevious, RollNext, RollAbove, RollBelow, RollExtendPrevious, RollExtendNext,
        RollExtendAbove, RollExtendBelow, RollSelectAll, RollClear, RollMoveEarlier, RollMoveLater, RollTransposeUp, RollTransposeDown,
        RollOctaveUp, RollOctaveDown, RollLonger, RollShorter, RollLouder, RollSofter, RollInsertEarlier, RollInsertLater, RollAddNote,
        RollDelete, RollDeleteAlternate, RollCopy, RollCut, RollPaste, RollDuplicate, RollQuantize, RollPedal, RollAudition, RollZoomIn,
        RollZoomOut,
    ];

    /// <summary>Every shortcut, in the order they are listed.</summary>
    public static readonly IReadOnlyList<Shortcut> All =
    [
        RenameProject, DeleteProject, Undo, Redo, RedoAlternate, RenameTrack, NudgeLeft, NudgeRight, NudgeUp, NudgeDown,
        OpenClip, AddMidiClip, MidiClipLonger, MidiClipShorter,
        PlayPause, Stop, ToStart, ToEnd, SeekBack, SeekForward, ToggleLoop, ToggleClick,
        DeleteMedia, AddMedia, MediaNext, MediaPrevious, MediaFirst, MediaLast, .. Roll,
    ];
}
