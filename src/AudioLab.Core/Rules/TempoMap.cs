using AudioLab.Core.Model;

namespace AudioLab.Core.Rules;

/// <summary>Converts between seconds and beats through the tempo changes. Beats are quarter notes counted from zero.</summary>
/// <remarks>Things placed in seconds stay put when the tempo changes; only the bars and beats move.</remarks>
public sealed class TempoMap
{
    private readonly (double Time, double Bpm, double Beat)[] _segments;

    private TempoMap((double Time, double Bpm, double Beat)[] segments) => _segments = segments;

    /// <summary>The tempo map of a document.</summary>
    public static TempoMap From(ProjectDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return From(document.Bpm, document.TempoMap);
    }

    /// <summary>The tempo map of a start tempo and the changes after it, which are used in time order.</summary>
    public static TempoMap From(double bpm, IEnumerable<TempoChange> changes)
    {
        ArgumentNullException.ThrowIfNull(changes);
        List<(double Time, double Bpm, double Beat)> segments = [(0, StartTempo(bpm), 0)];
        foreach (TempoChange change in changes.Where(change => change.Time > 1e-6 && change.Bpm > 0).OrderBy(change => change.Time))
        {
            (double time, double tempo, double beat) = segments[^1];
            segments.Add((change.Time, change.Bpm, beat + ((change.Time - time) * tempo / 60)));
        }
        return new TempoMap([.. segments]);
    }

    /// <summary>The tempo in force at <paramref name="time"/>, in beats per minute.</summary>
    public double BpmAt(double time) => AtTime(time).Bpm;

    /// <summary>How many quarter-note beats have passed at <paramref name="time"/>.</summary>
    public double BeatsAt(double time)
    {
        (double start, double bpm, double beat) = AtTime(time);
        return beat + ((time - start) * bpm / 60);
    }

    /// <summary>The time, in seconds, at which <paramref name="beat"/> quarter notes have passed.</summary>
    public double TimeAtBeat(double beat)
    {
        (double start, double bpm, double first) = AtBeat(beat);
        return start + ((beat - first) * 60 / bpm);
    }

    /// <summary>The seconds from <paramref name="time"/> to <paramref name="bars"/> bars later, through the tempo changes.</summary>
    public double BarsFrom(double time, double bars, TimeSignature signature)
    {
        return TimeAtBeat(BeatsAt(time) + (bars * MeterRules.BeatsPerBar(signature))) - time;
    }

    // A missing or zero tempo plays at 120 and a negative one at the slowest, as the old DAW did.
    private static double StartTempo(double bpm) => !double.IsFinite(bpm) || bpm == 0 ? 120 : Math.Max(1, bpm);

    private (double Time, double Bpm, double Beat) AtTime(double time)
    {
        (double Time, double Bpm, double Beat) found = _segments[0];
        foreach ((double Time, double Bpm, double Beat) segment in _segments)
        {
            if (segment.Time > time)
            {
                break;
            }
            found = segment;
        }
        return found;
    }

    private (double Time, double Bpm, double Beat) AtBeat(double beat)
    {
        (double Time, double Bpm, double Beat) found = _segments[0];
        foreach ((double Time, double Bpm, double Beat) segment in _segments)
        {
            if (segment.Beat > beat)
            {
                break;
            }
            found = segment;
        }
        return found;
    }
}
