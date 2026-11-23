using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Web.Session;

/// <summary>Where each bar starts, from the project's tempo map and time signature; the rule is Core's, never bar arithmetic here.</summary>
public static class BarGrid
{
    /// <summary>The bars of the arrangement.</summary>
    public const int MinimumBars = 24;

    /// <summary>The most bar lines drawn, so a very slow tempo or a very long song cannot make the ruler unbounded.</summary>
    public const int MaximumBars = 2000;

    /// <summary>Every bar that starts before <paramref name="endSeconds"/>, at least <see cref="MinimumBars"/>.</summary>
    public static IReadOnlyList<BarLine> Lines(ProjectDocument document, double endSeconds)
    {
        ArgumentNullException.ThrowIfNull(document);
        TempoMap tempo = TempoMap.From(document);
        double beatsPerBar = MeterRules.BeatsPerBar(document.TimeSignature);
        List<BarLine> lines = [];
        for (int bar = 1; bar <= MaximumBars; bar++)
        {
            double time = tempo.TimeAtBeat((bar - 1) * beatsPerBar);
            if (bar > MinimumBars && time >= endSeconds)
            {
                break;
            }
            lines.Add(new BarLine(bar, time, (bar - 1) % 4 == 0));
        }
        return lines;
    }

    /// <summary>The bar and beat a time falls in, as the counter writes it: bar, beat and the sixteenth of the beat, from 1.</summary>
    public static string Position(ProjectDocument document, double seconds)
    {
        ArgumentNullException.ThrowIfNull(document);
        TempoMap tempo = TempoMap.From(document);
        double beatsPerBar = MeterRules.BeatsPerBar(document.TimeSignature);
        double beats = Math.Max(0, tempo.BeatsAt(seconds));
        int bar = (int)(beats / beatsPerBar) + 1;
        double inBar = beats - ((bar - 1) * beatsPerBar);
        int beat = (int)inBar + 1;
        int sixteenth = (int)((inBar - (int)inBar) * 4) + 1;
        return $"{bar}.{beat}.{sixteenth}";
    }
}
