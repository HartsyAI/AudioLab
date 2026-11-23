using System.Globalization;
using System.Text;
using AudioLab.Core.Model;
using AudioLab.Web.Session.Roll;
using Microsoft.AspNetCore.Components;

namespace AudioLab.Web.Components.PianoRoll;

/// <summary>The notes of the clip as a few SVG paths, not an element each: one path for each step of velocity.</summary>
/// <remarks>
/// The drawing is in seconds across and pitch rows down and stretched over the clip, so a note is a rectangle of the path. It is built when the
/// notes or the scale change, and not for a selection or a gesture, which <see cref="RollSelection"/> and the overlay draw (D17, D48).
/// </remarks>
public sealed partial class RollNotes
{
    /// <summary>How many steps of velocity are drawn as brightness.</summary>
    public const int Levels = VelocityLevels.Count;

    /// <summary>The narrowest a note is drawn, in pixels.</summary>
    public const double MinPixels = 2;

    private const double Inset = 0.06;

    private MidiClip? _built;

    private double _scale;

    private bool _rebuilt;

    private string[] _levels = [];

    /// <summary>The clip.</summary>
    [Parameter, EditorRequired]
    public MidiClip Clip { get; set; } = null!;

    /// <summary>The scale.</summary>
    [Parameter]
    public RollGeometry Geometry { get; set; }

    /// <summary>The colour as a style value, already mapped to the nearest track token.</summary>
    [Parameter, EditorRequired]
    public string ColorToken { get; set; } = string.Empty;

    /// <summary>The step of brightness a velocity falls in, 0 for the softest to <see cref="Levels"/> less one.</summary>
    public static int Level(int velocity) => VelocityLevels.Of(velocity);

    /// <summary>The path data of rectangles for notes: runs that touch at the scale of the pixel are one rectangle, so the path is bounded.</summary>
    /// <remarks>A note is at least <see cref="MinPixels"/> wide. Notes of one pitch that touch or overlap at that resolution are one run.</remarks>
    public static string Rectangles(IEnumerable<MidiNote> notes, double clipLength, double pixelsPerSecond)
    {
        ArgumentNullException.ThrowIfNull(notes);
        return Runs(notes, clipLength, pixelsPerSecond, 1, _ => 0)[0].ToString();
    }

    /// <summary>The notes of a clip as one path for each step of velocity, in runs as <see cref="Rectangles"/> makes them.</summary>
    public static string[] ByLevel(IEnumerable<MidiNote> notes, double clipLength, double pixelsPerSecond)
    {
        ArgumentNullException.ThrowIfNull(notes);
        return [.. Runs(notes, clipLength, pixelsPerSecond, Levels, note => Level(note.Velocity)).Select(path => path.ToString())];
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
        _levels = ByLevel(Clip.Notes, Clip.Length, Geometry.PixelsPerSecond);
    }

    /// <inheritdoc />
    protected override bool ShouldRender() => _rebuilt;

    // Notes of one pitch in order of start, each pixel-snapped; a note that starts inside the run before it of the same level extends the run.
    private static StringBuilder[] Runs(
        IEnumerable<MidiNote> notes,
        double clipLength,
        double pixelsPerSecond,
        int levels,
        Func<MidiNote, int> levelOf)
    {
        StringBuilder[] paths = [.. Enumerable.Range(0, levels).Select(_ => new StringBuilder())];
        (double From, double To)?[] open = new (double, double)?[levels];
        int row = -1;
        foreach (MidiNote note in notes.OrderBy(note => note.Pitch).ThenBy(note => note.Start))
        {
            if (note.Pitch != row)
            {
                Flush(paths, open, row, pixelsPerSecond);
                row = note.Pitch;
            }
            int level = levelOf(note);
            double from = Math.Floor(note.Start * pixelsPerSecond);
            double to = Math.Max(from + MinPixels, Math.Ceiling(Math.Min(note.Start + note.Duration, clipLength) * pixelsPerSecond));
            if (open[level] is { } run && from <= run.To)
            {
                open[level] = (run.From, Math.Max(run.To, to));
                continue;
            }
            Flush(paths[level], open, level, row, pixelsPerSecond);
            open[level] = (from, to);
        }
        Flush(paths, open, row, pixelsPerSecond);
        return paths;
    }

    private static void Flush(StringBuilder[] paths, (double From, double To)?[] open, int row, double pixelsPerSecond)
    {
        for (int level = 0; level < open.Length; level++)
        {
            Flush(paths[level], open, level, row, pixelsPerSecond);
        }
    }

    private static void Flush(StringBuilder path, (double From, double To)?[] open, int level, int row, double pixelsPerSecond)
    {
        if (open[level] is not { } run)
        {
            return;
        }
        open[level] = null;
        double width = (run.To - run.From) / pixelsPerSecond;
        path.Append('M').Append(Format(run.From / pixelsPerSecond)).Append(' ').Append(Format(RollGeometry.Pitches - 1 - row + Inset))
            .Append('h').Append(Format(width)).Append('v').Append(Format(1 - (2 * Inset))).Append('h').Append('-').Append(Format(width)).Append('z');
    }

    private static string Format(double value) => value.ToString("0.####", CultureInfo.InvariantCulture);
}
