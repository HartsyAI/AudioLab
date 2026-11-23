using System.Globalization;
using System.Text;
using AudioLab.Core.Model;

namespace AudioLab.Web.Session;

/// <summary>Draws the notes of a MIDI clip as one SVG path: time across, pitch down, the clip's own pitch range over the height.</summary>
/// <remarks>
/// A note is a stroked line on a grid of whole drawing units, and the notes of one pitch that touch or overlap in a unit are one line, so the path
/// holds at most half a line for each unit and pitch whatever the clip holds. It is built in one pass over the notes and one over the grid.
/// </remarks>
public static class MidiPreview
{
    /// <summary>The width of the drawing's coordinate space, in whole units; the svg stretches it over the clip body.</summary>
    public const int Width = 1000;

    /// <summary>The height of the drawing's coordinate space.</summary>
    public const double Height = 100;

    private const double Top = 10;

    private const double Span = 80;

    /// <summary>The path data: a move and a horizontal line for each run of notes of one pitch; empty for a clip with no notes.</summary>
    public static string Path(MidiClip clip) => Build(clip).Path;

    /// <summary>The path and the steps it took (one for each note and each scanned unit of the grid), which do not depend on the clock.</summary>
    public static (string Path, long Work) Build(MidiClip clip)
    {
        ArgumentNullException.ThrowIfNull(clip);
        if (clip.Notes.Count == 0 || clip.Length <= 0)
        {
            return (string.Empty, 0);
        }
        int high = int.MinValue;
        int low = int.MaxValue;
        int[]?[] rows = new int[]?[128];
        long work = 0;
        foreach (MidiNote note in clip.Notes)
        {
            high = Math.Max(high, note.Pitch);
            low = Math.Min(low, note.Pitch);
            int first = Math.Clamp((int)Math.Floor(note.Start / clip.Length * Width), 0, Width - 1);
            double end = Math.Min(note.Start + note.Duration, clip.Length);
            int last = Math.Clamp((int)Math.Ceiling(end / clip.Length * Width), first + 1, Width);
            int[] row = rows[note.Pitch] ??= new int[Width + 1];
            row[first]++;
            row[last]--;
            work++;
        }
        StringBuilder path = new();
        double range = high - low;
        for (int pitch = 0; pitch < rows.Length; pitch++)
        {
            if (rows[pitch] is not { } row)
            {
                continue;
            }
            string y = Format(range == 0 ? Height / 2 : Top + ((high - pitch) / range * Span));
            int covering = 0;
            int from = -1;
            for (int unit = 0; unit <= Width; unit++)
            {
                covering += row[unit];
                if (covering > 0 && from < 0)
                {
                    from = unit;
                }
                else if (covering <= 0 && from >= 0)
                {
                    path.Append('M').Append(from).Append(' ').Append(y).Append('h').Append(unit - from);
                    from = -1;
                }
                work++;
            }
        }
        return (path.ToString(), work);
    }

    private static string Format(double value) => value.ToString("0.#", CultureInfo.InvariantCulture);
}
