using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Web.Session;

/// <summary>The keyboard path to moving a clip: Alt with an arrow key nudges it one grid step in time, or to the next track of its kind.</summary>
public static class ClipMoves
{
    /// <summary>The move an arrow key makes from the clip's place, or null when it moves nowhere (the project start, or no further track).</summary>
    public static ClipMove? Nudge(ProjectDocument document, ClipId clip, string key)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(key);
        int row = document.Tracks.ToList().FindIndex(track => track.Clips.Any(c => c.Id == clip) || track.MidiClips.Any(c => c.Id == clip));
        if (row < 0)
        {
            return null;
        }
        Track own = document.Tracks[row];
        double start = own.Clips.FirstOrDefault(c => c.Id == clip)?.Start ?? own.MidiClips.First(c => c.Id == clip).Start;
        TrackKind kind = own.Kind;
        switch (key)
        {
            case "ArrowLeft":
            case "ArrowRight":
                double step = GridRules.StepSeconds(document.View.Grid, TempoMap.From(document), document.TimeSignature, start);
                double moved = Math.Max(0, start + (key == "ArrowRight" ? step : -step));
                return Math.Abs(moved - start) < TimelineRules.NoMove ? null : new ClipMove(clip, moved, null);
            case "ArrowUp":
            case "ArrowDown":
                int direction = key == "ArrowDown" ? 1 : -1;
                for (int next = row + direction; next >= 0 && next < document.Tracks.Count; next += direction)
                {
                    if (document.Tracks[next].Kind == kind)
                    {
                        return new ClipMove(clip, start, document.Tracks[next].Id);
                    }
                }
                return null;
            default:
                return null;
        }
    }
}
