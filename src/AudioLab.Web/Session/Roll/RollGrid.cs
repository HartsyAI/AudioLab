using System.Globalization;
using System.Text;
using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Web.Session.Roll;

/// <summary>The lines under the notes of a clip, as SVG path data in the drawing's own units: seconds across and pitch rows down.</summary>
/// <remarks>Bars, beats and grid steps come from the project's tempo map, the same lines the arrangement draws and a drag snaps to.</remarks>
public static class RollGrid
{
    /// <summary>The most lines of one kind that are drawn.</summary>
    public const int LineLimit = 4000;

    private const double MinBarPixels = 6;

    private const double MinStepPixels = 6;

    private const double MinBeatPixels = 12;

    private static readonly bool[] _black = [false, true, false, true, false, false, true, false, true, false, true, false];

    /// <summary>The vertical lines of the clip at a scale: grid steps and beats (when they are far enough apart to read) and bars.</summary>
    /// <remarks>
    /// A kind of line that would be more than <see cref="LineLimit"/> is left out, and bar lines then skip bars so they stay far enough apart,
    /// so a long clip is drawn to its end with a bounded path.
    /// </remarks>
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
        double beatSeconds = 60 / tempo.BpmAt(from);
        StringBuilder steps = new();
        StringBuilder beats = new();
        StringBuilder bars = new();
        double step = snap.Step(from);
        if (step * pixelsPerSecond >= MinStepPixels && clip.Length / step <= LineLimit)
        {
            foreach (double time in snap.Lines(from, to, LineLimit))
            {
                Vertical(steps, time - from);
            }
        }
        bool drawBeats = beatSeconds * pixelsPerSecond >= MinBeatPixels && clip.Length / beatSeconds <= LineLimit;
        double firstBar = Math.Ceiling((startBeat / beatsPerBar) - 1e-9);
        double lastBar = Math.Floor((tempo.BeatsAt(to) / beatsPerBar) + 1e-9);
        double stride = Math.Max(1, Math.Ceiling(MinBarPixels / (beatsPerBar * beatSeconds * pixelsPerSecond)));
        stride = Math.Max(stride, Math.Ceiling((lastBar - firstBar + 1) / LineLimit));
        int drawn = 0;
        for (double bar = firstBar; bar <= lastBar && drawn++ < LineLimit; bar += stride)
        {
            Vertical(bars, tempo.TimeAtBeat(bar * beatsPerBar) - from);
        }
        if (drawBeats)
        {
            drawn = 0;
            for (double beat = Math.Ceiling(startBeat - 1e-9); tempo.TimeAtBeat(beat) <= to + 1e-9 && drawn++ < LineLimit; beat++)
            {
                double bar = beat / beatsPerBar;
                if (Math.Abs(bar - Math.Round(bar)) >= 1e-9)
                {
                    Vertical(beats, tempo.TimeAtBeat(beat) - from);
                }
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
                black.Append('M').Append(0).Append(' ').Append(Number(row)).Append('h').Append(Number(length))
                    .Append("v1h-").Append(Number(length)).Append('z');
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
