using AudioLab.Core.Model;

namespace AudioLab.Web.Session.Roll;

/// <summary>The notes of a MIDI clip laid out for finding them: by pitch and start for a pointer, in time order for the keyboard.</summary>
/// <remarks>
/// Each pitch is a row sorted by start, with its longest duration, so a point finds the notes under it by a binary search and a short walk back,
/// and a segment finds the notes it crosses the same way; neither reads a whole clip.
/// </remarks>
public sealed class NoteIndex
{
    private readonly MidiNote[][] _rows;

    private readonly double[] _longest;

    private readonly Dictionary<NoteId, int> _order;

    private readonly Dictionary<NoteId, int> _drawn;

    private NoteIndex(MidiNote[] ordered, MidiNote[][] rows, double[] longest, Dictionary<NoteId, int> drawn)
    {
        Ordered = ordered;
        _rows = rows;
        _longest = longest;
        _drawn = drawn;
        _order = new Dictionary<NoteId, int>(ordered.Length);
        for (int index = 0; index < ordered.Length; index++)
        {
            _order[ordered[index].Id] = index;
        }
    }

    /// <summary>The notes by start, then pitch, then id.</summary>
    public IReadOnlyList<MidiNote> Ordered { get; }

    /// <summary>The notes by pitch, then start, then id: the order the roll draws runs in, so it needs no sort.</summary>
    public IEnumerable<MidiNote> ByPitch => _rows.SelectMany(row => row);

    /// <summary>Indexes a clip.</summary>
    public static NoteIndex Of(MidiClip clip)
    {
        ArgumentNullException.ThrowIfNull(clip);
        Dictionary<NoteId, int> drawn = new(clip.Notes.Count);
        List<MidiNote>?[] lists = new List<MidiNote>?[RollGeometry.Pitches];
        double[] longest = new double[RollGeometry.Pitches];
        int position = 0;
        foreach (MidiNote note in clip.Notes)
        {
            drawn[note.Id] = position++;
            (lists[note.Pitch] ??= []).Add(note);
            longest[note.Pitch] = Math.Max(longest[note.Pitch], note.Duration);
        }
        MidiNote[][] rows = new MidiNote[RollGeometry.Pitches][];
        for (int pitch = 0; pitch < rows.Length; pitch++)
        {
            rows[pitch] = lists[pitch] is { } list ? [.. list.OrderBy(note => note.Start).ThenBy(note => note.Id.Value)] : [];
        }
        MidiNote[] ordered = [.. clip.Notes.OrderBy(note => note.Start).ThenBy(note => note.Pitch).ThenBy(note => note.Id.Value)];
        return new NoteIndex(ordered, rows, longest, drawn);
    }

    /// <summary>A note by id, or null.</summary>
    public MidiNote? Find(NoteId id) => _order.TryGetValue(id, out int at) ? Ordered[at] : null;

    /// <summary>The note under a point: the one drawn on top when several overlap; <paramref name="edge"/> says whether it is in the grip.</summary>
    /// <remarks>
    /// The roll draws a selected note over the others, then the louder levels over the softer, then the later notes of the clip over the earlier;
    /// the one a pointer takes is the one on top.
    /// </remarks>
    /// <param name="seconds">The time, from the clip start.</param>
    /// <param name="pitch">The pitch.</param>
    /// <param name="gripSeconds">How wide the grip at the right edge of a note is, in seconds; never more than half the note.</param>
    /// <param name="edge">True when the point is in the grip.</param>
    /// <param name="selected">The selected notes, which are drawn last; none when null.</param>
    public MidiNote? HitTest(double seconds, int pitch, double gripSeconds, out bool edge, IReadOnlySet<NoteId>? selected = null)
    {
        edge = false;
        if (pitch is < 0 or >= RollGeometry.Pitches)
        {
            return null;
        }
        MidiNote[] row = _rows[pitch];
        MidiNote? hit = null;
        (int Selected, int Level, int Clip) best = default;
        for (int at = LastStartingAtOrBefore(row, seconds); at >= 0; at--)
        {
            MidiNote note = row[at];
            if (note.Start + _longest[pitch] < seconds)
            {
                break;
            }
            if (seconds > note.Start + note.Duration)
            {
                continue;
            }
            (int, int, int) rank = (selected?.Contains(note.Id) == true ? 1 : 0, VelocityLevels.Of(note.Velocity), _drawn[note.Id]);
            if (hit is null || rank.CompareTo(best) > 0)
            {
                hit = note;
                best = rank;
            }
        }
        if (hit is { } found)
        {
            edge = seconds >= found.Start + found.Duration - Math.Min(gripSeconds, found.Duration / 2);
        }
        return hit;
    }

    /// <summary>The notes of one pitch that touch a span of time, ends included, in order of start.</summary>
    public IEnumerable<MidiNote> InSpan(int pitch, double from, double to)
    {
        if (pitch is < 0 or >= RollGeometry.Pitches)
        {
            yield break;
        }
        MidiNote[] row = _rows[pitch];
        double start = Math.Min(from, to);
        double end = Math.Max(from, to);
        for (int at = FirstStartingAtOrAfter(row, start - _longest[pitch]); at < row.Length && row[at].Start <= end; at++)
        {
            if (row[at].Start + row[at].Duration >= start)
            {
                yield return row[at];
            }
        }
    }

    /// <summary>The notes that touch a rectangle of time and pitch, ends included.</summary>
    public IEnumerable<NoteId> InRectangle(double from, double to, int lowPitch, int highPitch)
    {
        int low = Math.Clamp(Math.Min(lowPitch, highPitch), 0, RollGeometry.Pitches - 1);
        int high = Math.Clamp(Math.Max(lowPitch, highPitch), 0, RollGeometry.Pitches - 1);
        for (int pitch = low; pitch <= high; pitch++)
        {
            foreach (MidiNote note in InSpan(pitch, from, to))
            {
                yield return note.Id;
            }
        }
    }

    /// <summary>The note the keyboard moves to from <paramref name="current"/>, or null when the key moves nowhere.</summary>
    /// <param name="current">The note that has the cursor, or null for none.</param>
    /// <param name="key">ArrowLeft and ArrowRight walk in time; ArrowUp and ArrowDown go to the nearest note above or below.</param>
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

    // The index of the last note of a row that starts at or before a time, or -1.
    private static int LastStartingAtOrBefore(MidiNote[] row, double seconds)
    {
        int low = 0;
        int high = row.Length;
        while (low < high)
        {
            int middle = low + ((high - low) / 2);
            if (row[middle].Start <= seconds)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }
        return low - 1;
    }

    // The index of the first note of a row that starts at or after a time, or the length of the row.
    private static int FirstStartingAtOrAfter(MidiNote[] row, double seconds)
    {
        int low = 0;
        int high = row.Length;
        while (low < high)
        {
            int middle = low + ((high - low) / 2);
            if (row[middle].Start < seconds)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }
        return low;
    }

    // The nearest pitch in the direction that has a note, and among its notes the one that starts closest to this one.
    private NoteId? Vertical(MidiNote note, bool up)
    {
        int step = up ? 1 : -1;
        for (int pitch = note.Pitch + step; pitch is >= 0 and < RollGeometry.Pitches; pitch += step)
        {
            if (_rows[pitch] is { Length: > 0 } row)
            {
                return row.MinBy(candidate => Math.Abs(candidate.Start - note.Start)).Id;
            }
        }
        return null;
    }
}
