using System.Text;
using AudioLab.Core.Model;
using AudioLab.Web.Session.Roll;
using Microsoft.AspNetCore.Components;

namespace AudioLab.Web.Components.PianoRoll;

/// <summary>The notes of the clip as a few SVG paths, not an element each: one path for each step of velocity.</summary>
/// <remarks>
/// The drawing is in pixels, whole pixels, so a note is a rectangle of the path and a path is cheap to write. It is built when the notes or the
/// scale change, and not for a selection or a gesture, which <see cref="RollSelection"/> and the overlay draw (D17, D48).
/// </remarks>
public sealed partial class RollNotes
{
    /// <summary>How many steps of velocity are drawn as brightness.</summary>
    public const int Levels = VelocityLevels.Count;

    /// <summary>The narrowest a note is drawn, in pixels.</summary>
    public const long MinPixels = 2;

    /// <summary>The gap above and below a note inside its row, in pixels.</summary>
    public const long Inset = 1;

    private MidiClip? _built;

    private double _scale;

    private bool _rebuilt;

    private string[] _levels = [];

    /// <summary>The clip.</summary>
    [Parameter, EditorRequired]
    public MidiClip Clip { get; set; } = null!;

    /// <summary>The clip's notes laid out by pitch and start, which the paths are written from without sorting.</summary>
    [Parameter, EditorRequired]
    public NoteIndex Index { get; set; } = null!;

    /// <summary>The scale.</summary>
    [Parameter]
    public RollGeometry Geometry { get; set; }

    /// <summary>The colour as a style value, already mapped to the nearest track token.</summary>
    [Parameter, EditorRequired]
    public string ColorToken { get; set; } = string.Empty;

    /// <summary>The step of brightness a velocity falls in, 0 for the softest to <see cref="Levels"/> less one.</summary>
    public static int Level(int velocity) => VelocityLevels.Of(velocity);

    /// <summary>The path data of rectangles for notes, in pixels: runs that touch at the pixel are one rectangle, so the path is bounded.</summary>
    /// <remarks>A note is at least <see cref="MinPixels"/> wide. Notes of one pitch that touch or overlap at that resolution are one run.</remarks>
    /// <param name="notes">The notes.</param>
    /// <param name="geometry">The scale.</param>
    /// <param name="sorted">Whether the notes already come by pitch and then by start, which saves the sort.</param>
    public static string Rectangles(IEnumerable<MidiNote> notes, RollGeometry geometry, bool sorted = false)
    {
        ArgumentNullException.ThrowIfNull(notes);
        return Runs(notes, geometry, sorted, 1, _ => 0)[0].ToString();
    }

    /// <summary>The notes of a clip as one path for each step of velocity, in runs as <see cref="Rectangles"/> makes them.</summary>
    /// <param name="notes">The notes.</param>
    /// <param name="geometry">The scale.</param>
    /// <param name="sorted">Whether the notes already come by pitch and then by start, which saves the sort.</param>
    public static string[] ByLevel(IEnumerable<MidiNote> notes, RollGeometry geometry, bool sorted = false)
    {
        ArgumentNullException.ThrowIfNull(notes);
        return [.. Runs(notes, geometry, sorted, Levels, note => Level(note.Velocity)).Select(path => path.ToString())];
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        _rebuilt = false;
        if (_built is not null && _scale == Geometry.PixelsPerSecond && (ReferenceEquals(_built, Clip) || _built.Equals(Clip)))
        {
            return;
        }
        _rebuilt = true;
        _built = Clip;
        _scale = Geometry.PixelsPerSecond;
        _levels = ByLevel(Index.ByPitch, Geometry, sorted: true);
    }

    /// <inheritdoc />
    protected override bool ShouldRender() => _rebuilt;

    // Notes of one pitch in order of start, each snapped to whole pixels; a note that starts inside the run before it of the same level extends it.
    private static StringBuilder[] Runs(IEnumerable<MidiNote> notes, RollGeometry geometry, bool sorted, int levels, Func<MidiNote, int> levelOf)
    {
        StringBuilder[] paths = [.. Enumerable.Range(0, levels).Select(_ => new StringBuilder())];
        (long From, long To)?[] open = new (long, long)?[levels];
        double pixels = geometry.PixelsPerSecond;
        int row = -1;
        foreach (MidiNote note in sorted ? notes : notes.OrderBy(note => note.Pitch).ThenBy(note => note.Start))
        {
            if (note.Pitch != row)
            {
                Flush(paths, open, row, geometry);
                row = note.Pitch;
            }
            int level = levelOf(note);
            long from = (long)Math.Floor(note.Start * pixels);
            long to = Math.Max(from + MinPixels, (long)Math.Ceiling(Math.Min(note.Start + note.Duration, geometry.Length) * pixels));
            if (open[level] is { } run && from <= run.To)
            {
                open[level] = (run.From, Math.Max(run.To, to));
                continue;
            }
            Flush(paths[level], open, level, row, geometry);
            open[level] = (from, to);
        }
        Flush(paths, open, row, geometry);
        return paths;
    }

    private static void Flush(StringBuilder[] paths, (long From, long To)?[] open, int row, RollGeometry geometry)
    {
        for (int level = 0; level < open.Length; level++)
        {
            Flush(paths[level], open, level, row, geometry);
        }
    }

    private static void Flush(StringBuilder path, (long From, long To)?[] open, int level, int row, RollGeometry geometry)
    {
        if (open[level] is not { } run)
        {
            return;
        }
        open[level] = null;
        long width = run.To - run.From;
        long top = (long)geometry.RowTop(row) + Inset;
        long height = (long)geometry.RowHeight - (2 * Inset);
        path.Append('M').Append(run.From).Append(' ').Append(top).Append('h').Append(width).Append('v').Append(height).Append("h-").Append(width)
            .Append('z');
    }
}
