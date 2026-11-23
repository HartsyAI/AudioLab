using AudioLab.Core.Model;

namespace AudioLab.Web.Session.Roll;

/// <summary>The notes of a MIDI clip laid out for finding them: by pitch for a pointer, in time order for the keyboard. Built once for each revision of the clip.</summary>
public sealed class NoteIndex
{
    private readonly List<MidiNote>?[] _rows;

    private readonly Dictionary<NoteId, int> _order;

    private NoteIndex(MidiNote[] ordered, List<MidiNote>?[] rows)
    {
        Ordered = ordered;
        _rows = rows;
        _order = new Dictionary<NoteId, int>(ordered.Length);
        for (int index = 0; index < ordered.Length; index++)
        {
            _order[ordered[index].Id] = index;
        }
    }

    /// <summary>The notes by start, then pitch, then id.</summary>
    public IReadOnlyList<MidiNote> Ordered { get; }

    /// <summary>Indexes a clip.</summary>
    public static NoteIndex Of(MidiClip clip)
    {
        ArgumentNullException.ThrowIfNull(clip);
        MidiNote[] ordered = [.. clip.Notes.OrderBy(note => note.Start).ThenBy(note => note.Pitch).ThenBy(note => note.Id.Value)];
        List<MidiNote>?[] rows = new List<MidiNote>?[RollGeometry.Pitches];
        foreach (MidiNote note in ordered)
        {
            (rows[note.Pitch] ??= []).Add(note);
        }
        return new NoteIndex(ordered, rows);
    }

    /// <summary>A note by id, or null.</summary>
    public MidiNote? Find(NoteId id) => _order.TryGetValue(id, out int at) ? Ordered[at] : null;

    /// <summary>The note under a point, the one drawn last when several overlap; <paramref name="edge"/> says whether the point is in its right-hand grip.</summary>
    /// <param name="seconds">The time, from the clip start.</param>
    /// <param name="pitch">The pitch.</param>
    /// <param name="gripSeconds">How wide the grip at the right edge of a note is, in seconds; never more than half the note.</param>
    /// <param name="edge">True when the point is in the grip.</param>
    public MidiNote? HitTest(double seconds, int pitch, double gripSeconds, out bool edge)
    {
        edge = false;
        if (pitch is < 0 or >= RollGeometry.Pitches || _rows[pitch] is not { } row)
        {
            return null;
        }
        MidiNote? hit = null;
        foreach (MidiNote note in row)
        {
            if (seconds >= note.Start && seconds <= note.Start + note.Duration && (hit is null || _order[note.Id] > _order[hit.Value.Id]))
            {
                hit = note;
            }
        }
        if (hit is { } found)
        {
            edge = seconds >= found.Start + found.Duration - Math.Min(gripSeconds, found.Duration / 2);
        }
        return hit;
    }

    /// <summary>The notes that touch a rectangle of time and pitch, ends included.</summary>
    public IEnumerable<NoteId> InRectangle(double from, double to, int lowPitch, int highPitch)
    {
        int low = Math.Clamp(Math.Min(lowPitch, highPitch), 0, RollGeometry.Pitches - 1);
        int high = Math.Clamp(Math.Max(lowPitch, highPitch), 0, RollGeometry.Pitches - 1);
        double start = Math.Min(from, to);
        double end = Math.Max(from, to);
        for (int pitch = low; pitch <= high; pitch++)
        {
            if (_rows[pitch] is not { } row)
            {
                continue;
            }
            foreach (MidiNote note in row)
            {
                if (note.Start <= end && note.Start + note.Duration >= start)
                {
                    yield return note.Id;
                }
            }
        }
    }

    /// <summary>The note the keyboard moves to from <paramref name="current"/>, or null when the key moves nowhere.</summary>
    /// <param name="current">The note that has the cursor, or null for none.</param>
    /// <param name="key">ArrowLeft and ArrowRight go to the previous and next note in time; ArrowUp and ArrowDown to the nearest one above and below.</param>
    public NoteId? Walk(NoteId? current, string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (Ordered.Count == 0)
        {
            return null;
        }
        if (current is not { } from || !_order.TryGetValue(from, out int at))
        {
            return key == "ArrowLeft" ? Ordered[^1].Id : Ordered[0].Id;
        }
        MidiNote note = Ordered[at];
        return key switch
        {
            "ArrowLeft" => at > 0 ? Ordered[at - 1].Id : null,
            "ArrowRight" => at < Ordered.Count - 1 ? Ordered[at + 1].Id : null,
            "ArrowUp" => Vertical(note, up: true),
            "ArrowDown" => Vertical(note, up: false),
            _ => null,
        };
    }

    // The nearest pitch in the direction that has a note, and among its notes the one that starts closest to this one.
    private NoteId? Vertical(MidiNote note, bool up)
    {
        int step = up ? 1 : -1;
        for (int pitch = note.Pitch + step; pitch is >= 0 and < RollGeometry.Pitches; pitch += step)
        {
            if (_rows[pitch] is { Count: > 0 } row)
            {
                return row.MinBy(candidate => Math.Abs(candidate.Start - note.Start)).Id;
            }
        }
        return null;
    }
}
