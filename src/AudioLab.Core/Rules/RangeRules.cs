using AudioLab.Core.Model;

namespace AudioLab.Core.Rules;

/// <summary>The range rules of the old DAW: what a range delete and a range split do to the clips of a track. Times are seconds.</summary>
public static class RangeRules
{
    /// <summary>The shortest range, and the shortest piece of a clip a range delete leaves.</summary>
    public const double MinLength = 0.001;

    /// <summary>Clips that touch a range only within this many seconds are not cut by it.</summary>
    public const double Tolerance = 1e-6;

    /// <summary>Whether a clip has audio inside the range, which is how the old DAW told a clip a delete cuts from one it leaves alone.</summary>
    public static bool Overlaps(AudioClip clip, double start, double end)
    {
        ArgumentNullException.ThrowIfNull(clip);
        return !(ClipRules.EndOf(clip) <= start + Tolerance || clip.Start >= end - Tolerance);
    }

    /// <summary>Whether a MIDI clip lies partly inside the range and partly outside it, so that deleting the range cuts into its notes.</summary>
    public static bool Cuts(MidiClip clip, double start, double end)
    {
        ArgumentNullException.ThrowIfNull(clip);
        bool overlaps = clip.Start < end - Tolerance && ClipRules.EndOf(clip) > start + Tolerance;
        return overlaps && !Inside(clip, start, end);
    }

    /// <summary>The clips of a track with the range removed.</summary>
    /// <remarks>
    /// A clip across an edge is cut back to the part outside, a clip across the whole range is cut into two, and a clip inside is gone.
    /// With <paramref name="ripple"/> every clip that starts at or after the end moves earlier by the length of the range, which closes
    /// the gap. A piece shorter than <see cref="MinLength"/> is dropped. A clip that keeps one piece keeps its id and everything else; one that
    /// keeps two gives the left piece its id and metadata and the right piece a new id and only the lyrics, as a split does.
    /// </remarks>
    public static ValueList<AudioClip> Delete(IReadOnlyList<AudioClip> clips, double start, double end, bool ripple, Func<ClipId> newId)
    {
        ArgumentNullException.ThrowIfNull(clips);
        ArgumentNullException.ThrowIfNull(newId);
        double length = end - start;
        List<AudioClip> kept = [];
        foreach (AudioClip clip in clips)
        {
            if (!Overlaps(clip, start, end))
            {
                kept.Add(clip);
                continue;
            }
            double clipEnd = ClipRules.EndOf(clip);
            bool left = start - clip.Start >= MinLength;
            bool right = clipEnd - end >= MinLength;
            if (left)
            {
                kept.Add(LeftPiece(clip, start));
            }
            if (right)
            {
                AudioClip piece = RightPiece(clip, end);
                kept.Add(left ? piece with { Id = newId(), Meta = ClipRules.LyricsOnly(clip.Meta) } : piece);
            }
        }
        AudioClip Close(AudioClip clip) => clip.Start >= end - Tolerance ? clip with { Start = Math.Max(0, clip.Start - length) } : clip;
        return [.. ripple ? kept.Select(Close) : kept];
    }

    /// <summary>The MIDI clips of a track with those wholly inside the range gone and, with ripple, the later ones moved earlier.</summary>
    /// <remarks>A clip that the range cuts into is left as it is; <see cref="Cuts"/> tells which those are.</remarks>
    public static ValueList<MidiClip> Delete(IReadOnlyList<MidiClip> clips, double start, double end, bool ripple)
    {
        ArgumentNullException.ThrowIfNull(clips);
        double length = end - start;
        return
        [
            .. clips
                .Where(clip => !Inside(clip, start, end))
                .Select(clip => ripple && clip.Start >= end - Tolerance ? clip with { Start = Math.Max(0, clip.Start - length) } : clip),
        ];
    }

    /// <summary>The clips of a track cut at both edges of the range, each cut a split: the left piece keeps the id, the right one is new.</summary>
    public static ValueList<AudioClip> Split(IReadOnlyList<AudioClip> clips, double start, double end, Func<ClipId> newId)
    {
        ArgumentNullException.ThrowIfNull(clips);
        ArgumentNullException.ThrowIfNull(newId);
        List<AudioClip> result = [];
        foreach (AudioClip clip in clips)
        {
            List<AudioClip> pieces = [clip];
            foreach (double at in new[] { start, end })
            {
                List<AudioClip> next = [];
                foreach (AudioClip piece in pieces)
                {
                    if (ClipRules.CanSplit(piece, at))
                    {
                        (AudioClip a, AudioClip b) = ClipRules.Split(piece, at, newId());
                        next.AddRange([a, b]);
                    }
                    else
                    {
                        next.Add(piece);
                    }
                }
                pieces = next;
            }
            result.AddRange(pieces);
        }
        return [.. result];
    }

    /// <summary>The name a clip takes from the section it starts in: the section's name, a dash, the track's name; cut to the name limit.</summary>
    public static string ClipName(Section section, string trackName)
    {
        ArgumentNullException.ThrowIfNull(section);
        ArgumentNullException.ThrowIfNull(trackName);
        string name = $"{section.Name} - {trackName}";
        return name.Length > ProjectLimits.MaxName ? name[..ProjectLimits.MaxName].TrimEnd() : name;
    }

    private static bool Inside(MidiClip clip, double start, double end)
    {
        return clip.Start >= start - Tolerance && ClipRules.EndOf(clip) <= end + Tolerance;
    }

    private static AudioClip LeftPiece(AudioClip clip, double start)
    {
        double length = start - clip.Start;
        return clip with
        {
            TrimEnd = clip.Duration - clip.Offset - length,
            FadeIn = Math.Min(clip.FadeIn, length),
            FadeOut = 0,
        };
    }

    private static AudioClip RightPiece(AudioClip clip, double end)
    {
        double cut = end - clip.Start;
        double length = ClipRules.EndOf(clip) - end;
        return clip with
        {
            Start = end,
            Offset = clip.Offset + cut,
            FadeIn = 0,
            FadeOut = Math.Min(clip.FadeOut, length),
        };
    }
}
