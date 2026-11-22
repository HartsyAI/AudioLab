using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Web.Session;

/// <summary>How far the arrangement reaches: the end of its last clip or section, and a few empty bars after it, so there is room to place something.</summary>
public static class SessionExtent
{
    private const int TrailingBars = 4;

    /// <summary>The seconds the lanes cover, at least the minimum number of bars.</summary>
    public static double LengthSeconds(ProjectDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        double end = 0;
        foreach (Track track in document.Tracks)
        {
            foreach (AudioClip clip in track.Clips)
            {
                end = Math.Max(end, ClipRules.EndOf(clip));
            }
            foreach (MidiClip clip in track.MidiClips)
            {
                end = Math.Max(end, ClipRules.EndOf(clip));
            }
        }
        foreach (Section section in document.Sections)
        {
            end = Math.Max(end, section.End);
        }
        foreach (Marker marker in document.Markers)
        {
            end = Math.Max(end, marker.Time);
        }
        TempoMap tempo = TempoMap.From(document);
        double beatsPerBar = MeterRules.BeatsPerBar(document.TimeSignature);
        double bars = Math.Ceiling(tempo.BeatsAt(end) / beatsPerBar) + TrailingBars;
        double minimum = tempo.TimeAtBeat(BarGrid.MinimumBars * beatsPerBar);
        double span = tempo.TimeAtBeat(Math.Min(bars, BarGrid.MaximumBars) * beatsPerBar);
        return Math.Max(minimum, span);
    }
}
