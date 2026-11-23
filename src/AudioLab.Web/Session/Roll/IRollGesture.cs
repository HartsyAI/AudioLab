using AudioLab.Core.Model;

namespace AudioLab.Web.Session.Roll;

/// <summary>A pointer gesture in the piano roll: it follows the pointer, shows where it would land, and on release says what to send.</summary>
public interface IRollGesture
{
    /// <summary>Whether the pointer has travelled far enough for this to be a drag and not a click.</summary>
    public bool Active { get; }

    /// <summary>How many of the notes or events it acts on were gone from the clip at release; <see cref="Commit"/> sets it.</summary>
    public int Lost { get; }

    /// <summary>Moves the pointer to a point in pixels of the drawing the gesture began in.</summary>
    public void Update(double x, double y);

    /// <summary>Releases: the edit to send, or null when nothing changed, so a gesture that ends where it began leaves undo as it was.</summary>
    /// <param name="current">The clip as the page holds it now; the edit is rebased on it, as it may differ from the one dragged.</param>
    public RollEdit? Commit(MidiClip current);
}
