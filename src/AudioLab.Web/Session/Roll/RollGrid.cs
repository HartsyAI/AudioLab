using System.Globalization;
using System.Text;
using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Web.Session.Roll;

/// <summary>The lines under the notes of a clip, as SVG path data in the drawing's own units: seconds across and pitch rows down.</summary>
/// <remarks>Bars, beats and grid steps come from the project's tempo map, the same lines the arrangement draws and a drag snaps to.</remarks>
public static class RollGrid
{
    private const int LineLimit = 4000;

    private const double MinStepPixels = 6;

    private const double MinBeatPixels = 12;

    private static readonly bool[] _black = [false, true, false, true, false, false, true, false, true, false, true, false];

    /// <summary>The vertical lines of the clip at a scale: grid steps (when they are far enough apart to read), beats and bars.</summary>
    public static RollLines Lines(ProjectDocument document, MidiClip clip, double pixelsPerSecond)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(clip);
        TempoMap tempo = TempoMap.From(document);
        GridSnap snap = new(document);
        double from = clip.Start;
        double to = clip.Start + clip.Length;
        double beatsPerBar = MeterRules.BeatsPerBar(document.TimeSignature);
        double startBeat = tempo.BeatsAt(from);
        StringBuilder steps = new();
        StringBuilder beats = new();
        StringBuilder bars = new();
        if (snap.Step(from) * pixelsPerSecond >= MinStepPixels)
        {
            foreach (double time in snap.Lines(from, to, LineLimit))
            {
                Vertical(steps, time - from);
            }
        }
        bool drawBeats = 60 / tempo.BpmAt(from) * pixelsPerSecond >= MinBeatPixels;
        int count = 0;
        for (double beat = Math.Ceiling(startBeat - 1e-9); count < LineLimit; beat++, count++)
        {
            double time = tempo.TimeAtBeat(beat);
            if (time > to + 1e-9)
            {
                break;
            }
            bool bar = Math.Abs((beat / beatsPerBar) - Math.Round(beat / beatsPerBar)) < 1e-9;
            if (bar)
            {
                Vertical(bars, time - from);
            }
            else if (drawBeats)
            {
                Vertical(beats, time - from);
            }
        }
        return new RollLines(steps.ToString(), beats.ToString(), bars.ToString());
    }

    /// <summary>The bands of the black keys and the lines between octaves, in rows from the top; they do not change with the clip.</summary>
    public static (string BlackRows, string Octaves) Rows(double length)
    {
        StringBuilder black = new();
        StringBuilder octaves = new();
        for (int pitch = 0; pitch < RollGeometry.Pitches; pitch++)
        {
            double row = RollGeometry.Pitches - 1 - pitch;
            if (_black[pitch % 12])
            {
                black.Append('M').Append(0).Append(' ').Append(Number(row)).Append('h').Append(Number(length)).Append("v1h-").Append(Number(length)).Append('z');
            }
            if (pitch % 12 == 0)
            {
                octaves.Append('M').Append(0).Append(' ').Append(Number(row + 1)).Append('h').Append(Number(length));
            }
        }
        return (black.ToString(), octaves.ToString());
    }

    /// <summary>Whether a pitch is a black key.</summary>
    public static bool IsBlack(int pitch) => _black[pitch % 12];

    private static void Vertical(StringBuilder path, double seconds)
    {
        path.Append('M').Append(Number(seconds)).Append(" 0V").Append(RollGeometry.Pitches);
    }

    private static string Number(double value) => value.ToString("0.####", CultureInfo.InvariantCulture);
}
