using AudioLab.Core.Model;

namespace AudioLab.Core.Rules;

/// <summary>Which part of each audio clip on a track is heard when clips overlap: a later clip covers the ones before it.</summary>
/// <remarks>The old DAW's `trackAudibleSegments`; playback, export and the lyrics strip all read it, so they agree on the take that plays.</remarks>
public static class LayerRules
{
    /// <summary>A click-free ramp where a covering clip starts or ends, in seconds.</summary>
    public const double SeamFade = 0.004;

    // Pieces shorter than this are not worth playing; the old DAW dropped them as well.
    private const double MinPiece = 0.002;

    // Two edges this close are the same edge.
    private const double EdgeTolerance = 1e-6;

    // How far a crossfade's fade may miss the overlap and still count as one.
    private const double CrossfadeTolerance = 1e-3;

    /// <summary>The stretches each unmuted clip is heard, clip by clip in track order and in time order inside a clip.</summary>
    /// <remarks>
    /// A muted clip plays nothing and covers nothing. Where a clip ends inside the fade-in of the clip above it while fading out over
    /// the overlap (or starts inside that clip's fade-out while fading in), both play there: a crossfade.
    /// </remarks>
    public static IReadOnlyList<AudibleSpan> AudibleSpans(IReadOnlyList<AudioClip> clips)
    {
        ArgumentNullException.ThrowIfNull(clips);
        double[] starts = new double[clips.Count];
        double[] ends = new double[clips.Count];
        double[] fadeIn = new double[clips.Count];
        double[] fadeOut = new double[clips.Count];
        for (int i = 0; i < clips.Count; i++)
        {
            AudioClip clip = clips[i];
            double visible = ClipRules.VisibleLength(clip);
            starts[i] = clip.Start;
            ends[i] = clip.Start + visible;
            fadeIn[i] = Math.Min(clip.FadeIn, visible);
            fadeOut[i] = Math.Min(clip.FadeOut, visible - fadeIn[i]);
        }
        List<AudibleSpan> spans = [];
        for (int i = 0; i < clips.Count; i++)
        {
            if (clips[i].Muted || ends[i] - starts[i] <= 0)
            {
                continue;
            }
            List<(double Start, double End)> pieces = [(starts[i], ends[i])];
            for (int j = i + 1; j < clips.Count && pieces.Count > 0; j++)
            {
                if (clips[j].Muted)
                {
                    continue;
                }
                (double coverStart, double coverEnd) = CoverOf(i, j, starts, ends, fadeIn, fadeOut);
                if (coverEnd - coverStart > MinPiece)
                {
                    pieces = Cut(pieces, coverStart, coverEnd);
                }
            }
            foreach ((double start, double end) in pieces)
            {
                spans.Add(new AudibleSpan(clips[i].Id, start, end, start > starts[i] + EdgeTolerance, end < ends[i] - EdgeTolerance));
            }
        }
        return spans;
    }

    // What clip j covers of clip i; a crossfade shrinks the cover so that both play over the overlap.
    private static (double Start, double End) CoverOf(int i, int j, double[] starts, double[] ends, double[] fadeIn, double[] fadeOut)
    {
        double coverStart = starts[j];
        double coverEnd = ends[j];
        if (ends[i] > coverStart && ends[i] <= coverStart + fadeIn[j] + EdgeTolerance
            && fadeOut[i] > 0 && ends[i] - fadeOut[i] <= coverStart + CrossfadeTolerance)
        {
            coverStart = ends[i];
        }
        if (starts[i] < coverEnd && starts[i] >= coverEnd - fadeOut[j] - EdgeTolerance
            && fadeIn[i] > 0 && starts[i] + fadeIn[i] >= coverEnd - CrossfadeTolerance)
        {
            coverEnd = starts[i];
        }
        return (coverStart, coverEnd);
    }

    private static List<(double Start, double End)> Cut(List<(double Start, double End)> pieces, double coverStart, double coverEnd)
    {
        List<(double Start, double End)> next = [];
        foreach ((double start, double end) in pieces)
        {
            if (coverEnd <= start || coverStart >= end)
            {
                next.Add((start, end));
                continue;
            }
            if (coverStart > start && coverStart - start > MinPiece)
            {
                next.Add((start, coverStart));
            }
            if (coverEnd < end && end - coverEnd > MinPiece)
            {
                next.Add((coverEnd, end));
            }
        }
        return next;
    }
}
