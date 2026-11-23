using AudioLab.Web.Interop;
using AudioLab.Web.Services;

namespace AudioLab.Web.Session.Roll;

/// <summary>Which of the roll's keys a press is, and whether it does anything now.</summary>
/// <remarks>
/// A key of the roll is taken whether or not it runs, so the browser and the transport do not act on it. It does not run while a gesture
/// holds the pointer, and a key that is held down runs once: only the keys that walk, move the insert point or zoom, which send nothing,
/// repeat. An edit that repeated would send the same targets again from a document that has not changed yet.
/// </remarks>
public static class RollKeyPolicy
{
    private static readonly HashSet<string> _repeatable =
    [
        Shortcuts.RollPrevious.Id, Shortcuts.RollNext.Id, Shortcuts.RollAbove.Id, Shortcuts.RollBelow.Id, Shortcuts.RollExtendPrevious.Id,
        Shortcuts.RollExtendNext.Id, Shortcuts.RollExtendAbove.Id, Shortcuts.RollExtendBelow.Id, Shortcuts.RollInsertEarlier.Id,
        Shortcuts.RollInsertLater.Id, Shortcuts.RollZoomIn.Id, Shortcuts.RollZoomOut.Id,
    ];

    /// <summary>The command of the roll that a press is, or null when it is not one of the roll's.</summary>
    public static Shortcut? Find(KeyChord chord) => Shortcuts.Roll.FirstOrDefault(shortcut => shortcut.Matches(chord));

    /// <summary>Whether a press of a command of the roll does it now.</summary>
    /// <param name="shortcut">The command.</param>
    /// <param name="chord">The press.</param>
    /// <param name="gestureActive">Whether a pointer is down in a gesture.</param>
    public static bool Runs(Shortcut shortcut, KeyChord chord, bool gestureActive)
    {
        ArgumentNullException.ThrowIfNull(shortcut);
        return !gestureActive && (!chord.Repeat || _repeatable.Contains(shortcut.Id));
    }
}
