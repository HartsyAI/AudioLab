using AudioLab.Core.Commands;
using AudioLab.Core.Model;

namespace AudioLab.Web.Session.Roll;

/// <summary>A drag on the velocity of a note: the height of the pointer in the lane is the velocity, for the note and its selection.</summary>
public sealed class VelocityDrag : IRollGesture
{
    private readonly ClipId _clip;

    private readonly MidiNote[] _notes;

    private readonly double _height;

    /// <summary>Starts at a press at <paramref name="y"/> pixels from the top of a lane <paramref name="height"/> pixels tall.</summary>
    /// <param name="clip">The clip.</param>
    /// <param name="notes">The notes that take the velocity: the one pressed, or the selection it is in.</param>
    /// <param name="height">The lane's height in pixels.</param>
    /// <param name="y">Where the press is.</param>
    public VelocityDrag(ClipId clip, IEnumerable<MidiNote> notes, double height, double y)
    {
        ArgumentNullException.ThrowIfNull(notes);
        _clip = clip;
        _notes = [.. notes];
        _height = height;
        Update(0, y);
    }

    /// <summary>The velocity the notes would have if released now.</summary>
    public int Velocity { get; private set; }

    /// <summary>The notes that take it.</summary>
    public IReadOnlyList<MidiNote> Notes => _notes;

    /// <summary>A press already sets a velocity, so it is always a drag.</summary>
    public bool Active => true;

    /// <summary>The velocity a height in a lane means: the top is 127, the bottom 1.</summary>
    public static int At(double y, double height)
    {
        return height > 0 ? Math.Clamp((int)Math.Round(127 * (1 - (y / height))), 1, 127) : 1;
    }

    /// <inheritdoc />
    public void Update(double x, double y)
    {
        Velocity = At(y, _height);
    }

    /// <inheritdoc />
    public RollEdit? Commit()
    {
        if (_notes.All(note => note.Velocity == Velocity))
        {
            return null;
        }
        ValueList<NoteId> ids = [.. _notes.Select(note => note.Id)];
        int velocity = Velocity;
        ClipId clip = _clip;
        return RollEdit.Of(project => new SetNoteVelocity { Project = project, Clip = clip, Notes = ids, Velocity = velocity });
    }
}
