using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Web.Session;

/// <summary>Where a new MIDI clip goes and how long it is, and the length a clip takes when it is stepped by bars: all of it through the tempo map.</summary>
public static class MidiClipPlan
{
    private const double BarTolerance = 1e-6;

    /// <summary>The MIDI clip with this id and the track that holds it, or null when the document has none.</summary>
    public static (Track Track, MidiClip Clip)? Find(ProjectDocument document, ClipId clip)
    {
        ArgumentNullException.ThrowIfNull(document);
        foreach (Track track in document.Tracks)
        {
            foreach (MidiClip candidate in track.MidiClips)
            {
                if (candidate.Id == clip)
                {
                    return (track, candidate);
                }
            }
        }
        return null;
    }

    /// <summary>The length of a new clip that starts at <paramref name="start"/>: one bar at the tempo there.</summary>
    public static double DefaultLength(ProjectDocument document, double start)
    {
        ArgumentNullException.ThrowIfNull(document);
        return TempoMap.From(document).BarsFrom(start, 1, document.TimeSignature);
    }

    /// <summary>Where a new clip on a track starts when no place was pointed at: the first bar line after its last MIDI clip, or 0.</summary>
    public static double NextStart(ProjectDocument document, Track track)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(track);
        if (track.MidiClips.Count == 0)
        {
            return 0;
        }
        TempoMap tempo = TempoMap.From(document);
        double beatsPerBar = MeterRules.BeatsPerBar(document.TimeSignature);
        double end = track.MidiClips.Max(ClipRules.EndOf);
        double bars = Math.Ceiling((tempo.BeatsAt(end) / beatsPerBar) - BarTolerance);
        return tempo.TimeAtBeat(bars * beatsPerBar);
    }

    /// <summary>The length a clip has after its end moves by <paramref name="bars"/> bars (negative to shorten), or null below the least length.</summary>
    public static double? Stepped(ProjectDocument document, MidiClip clip, int bars)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(clip);
        TempoMap tempo = TempoMap.From(document);
        double end = clip.Start + clip.Length;
        double length = tempo.BarsFrom(end, bars, document.TimeSignature) + clip.Length;
        return length >= MidiClip.MinLength ? length : null;
    }

    /// <summary>How many bars long the clip is at its place, for a readout.</summary>
    public static double Bars(ProjectDocument document, MidiClip clip)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(clip);
        TempoMap tempo = TempoMap.From(document);
        return (tempo.BeatsAt(clip.Start + clip.Length) - tempo.BeatsAt(clip.Start)) / MeterRules.BeatsPerBar(document.TimeSignature);
    }
}
