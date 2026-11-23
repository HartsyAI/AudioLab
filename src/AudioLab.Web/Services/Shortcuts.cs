namespace AudioLab.Web.Services;

/// <summary>The one registry of shortcuts. A shortcut is never written into a component: menus, tooltips and key handlers read it here.</summary>
public static class Shortcuts
{
    /// <summary>The scope of the project list.</summary>
    public const string Projects = "projects";

    /// <summary>The scope of the session screen.</summary>
    public const string Session = "session";

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

    /// <summary>Every shortcut, in the order they are listed.</summary>
    public static readonly IReadOnlyList<Shortcut> All =
    [
        RenameProject, DeleteProject, Undo, Redo, RedoAlternate, RenameTrack, NudgeLeft, NudgeRight, NudgeUp, NudgeDown,
        OpenClip, AddMidiClip, MidiClipLonger, MidiClipShorter,
        PlayPause, Stop, ToStart, ToEnd, SeekBack, SeekForward, ToggleLoop, ToggleClick,
    ];
}
