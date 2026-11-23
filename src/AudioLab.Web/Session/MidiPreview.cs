using System.Globalization;
using System.Text;
using AudioLab.Core.Model;

namespace AudioLab.Web.Session;

/// <summary>Draws the notes of a MIDI clip as one SVG path: time across, pitch down, the clip's own pitch range over the height.</summary>
/// <remarks>The path is one stroked line for each note, as thick as a pixel count says whatever the clip size; no element per note.</remarks>
public static class MidiPreview
{
    /// <summary>The width of the drawing's coordinate space; the svg stretches it over the clip body.</summary>
    public const double Width = 1000;

    /// <summary>The height of the drawing's coordinate space.</summary>
    public const double Height = 100;

    private const double Top = 10;

    private const double Span = 80;

    private const double MinWidth = 3;

    /// <summary>The path data: a move and a horizontal line for each note, in the clip's note order; empty for a clip with no notes.</summary>
    public static string Path(MidiClip clip)
    {
        ArgumentNullException.ThrowIfNull(clip);
        if (clip.Notes.Count == 0 || clip.Length <= 0)
        {
            return string.Empty;
        }
        int high = int.MinValue;
        int low = int.MaxValue;
        foreach (MidiNote note in clip.Notes)
        {
            high = Math.Max(high, note.Pitch);
            low = Math.Min(low, note.Pitch);
        }
        double range = high - low;
        StringBuilder path = new(clip.Notes.Count * 16);
        foreach (MidiNote note in clip.Notes)
        {
            double x = Math.Clamp(note.Start / clip.Length * Width, 0, Width);
            double y = range == 0 ? Height / 2 : Top + ((high - note.Pitch) / range * Span);
            double width = Math.Max(MinWidth, Math.Min(note.Duration, clip.Length - note.Start) / clip.Length * Width);
            path.Append('M').Append(Format(x)).Append(' ').Append(Format(y)).Append('h').Append(Format(width));
        }
        return path.ToString();
    }

    private static string Format(double value) => value.ToString("0.#", CultureInfo.InvariantCulture);
}
