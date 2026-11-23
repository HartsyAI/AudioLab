using System.Globalization;
using System.Text.RegularExpressions;
using AudioLab.Core.Model;

namespace AudioLab.Core.Rules;

/// <summary>The clip rules: what a clip plays, how a split, a trim and a copy change it. Times are seconds.</summary>
public static partial class ClipRules
{
    /// <summary>The shortest part of an audio clip that a trim may leave.</summary>
    public const double MinClipLength = 0.05;

    /// <summary>A split this close to either end of a clip, in seconds, is not a split.</summary>
    public const double SplitMargin = 0.001;

    /// <summary>A copy of a clip starts this long after the original ends, in seconds.</summary>
    public const double CopyGap = 0.5;

    /// <summary>The part of the source file an audio clip plays, in seconds.</summary>
    public static double VisibleLength(AudioClip clip)
    {
        ArgumentNullException.ThrowIfNull(clip);
        return clip.Duration - clip.Offset - clip.TrimEnd;
    }

    /// <summary>Where an audio clip ends on the timeline.</summary>
    public static double EndOf(AudioClip clip) => clip.Start + VisibleLength(clip);

    /// <summary>Where a MIDI clip ends on the timeline.</summary>
    public static double EndOf(MidiClip clip)
    {
        ArgumentNullException.ThrowIfNull(clip);
        return clip.Start + clip.Length;
    }

    /// <summary>Whether a piece of a clip that long is kept by a split or a range delete: at least <see cref="SplitMargin"/>, within <see cref="ProjectLimits.Tolerance"/>.</summary>
    /// <remarks>The tolerance is only for the rounding of a subtraction, so that exactly 4 to 4.001 counts as a millisecond. This is the one minimum-piece rule of the split and the range commands.</remarks>
    public static bool FitsPiece(double length) => length >= SplitMargin - ProjectLimits.Tolerance;

    /// <summary>Whether <paramref name="at"/> is inside the clip, leaving a piece of at least <see cref="SplitMargin"/> on both sides (see <see cref="FitsPiece"/>).</summary>
    public static bool CanSplit(AudioClip clip, double at)
    {
        double inside = at - clip.Start;
        return FitsPiece(inside) && FitsPiece(VisibleLength(clip) - inside);
    }

    /// <summary>Two clips that together play what <paramref name="clip"/> played, cut at <paramref name="at"/>.</summary>
    /// <remarks>The left piece keeps the id and everything else; the right piece takes <paramref name="rightId"/> and only the lyrics, as a copy does.</remarks>
    /// <remarks>The audio is not touched: both pieces share the file and differ in the window they play.</remarks>
    public static (AudioClip Left, AudioClip Right) Split(AudioClip clip, double at, ClipId rightId)
    {
        ArgumentNullException.ThrowIfNull(clip);
        if (!CanSplit(clip, at))
        {
            throw new ArgumentOutOfRangeException(nameof(at), "The split point must be inside the clip.");
        }
        double inside = at - clip.Start;
        double rightOffset = clip.Offset + inside;
        AudioClip left = clip with
        {
            TrimEnd = clip.Duration - clip.Offset - inside,
            FadeIn = Math.Min(clip.FadeIn, inside),
            FadeOut = 0,
        };
        AudioClip right = clip with
        {
            Id = rightId,
            Start = at,
            Offset = rightOffset,
            FadeIn = 0,
            FadeOut = Math.Min(clip.FadeOut, clip.Duration - rightOffset - clip.TrimEnd),
            Meta = LyricsOnly(clip.Meta),
        };
        return (left, right);
    }

    /// <summary>The clip playing another media file in the same place, keeping the part it played as far as the new file allows.</summary>
    /// <remarks>
    /// The start, name, colour, gain and mute stay. The offset stays unless it leaves less than <see cref="MinClipLength"/> of the new file,
    /// then it is 0; the visible length stays unless the new file is shorter, then it is cut to it, and fades that no longer fit are
    /// shortened. A file under <see cref="MinClipLength"/> plays whole. The metadata is not touched here: the command drops the lyrics (their
    /// times are seconds of the old file) with the stems that follow them, and keeps the score and stem records. The same file and length
    /// give back the same instance.
    /// </remarks>
    public static AudioClip ReplaceMedia(AudioClip clip, MediaId media, double duration)
    {
        ArgumentNullException.ThrowIfNull(clip);
        if (clip.Media == media && clip.Duration == duration)
        {
            return clip;
        }
        double offset = clip.Offset >= duration - MinClipLength ? 0 : clip.Offset;
        double kept = Math.Min(VisibleLength(clip), duration - offset);
        return FitFades(clip with { Media = media, Duration = duration, Offset = offset, TrimEnd = duration - offset - kept });
    }

    /// <summary>Moves the start edge to <paramref name="edge"/> on the timeline.</summary>
    /// <remarks>The audio after it stays where it was; the clip cannot start before 0 or shrink below <see cref="MinClipLength"/>. Fades that no longer fit are shortened.</remarks>
    public static AudioClip TrimStart(AudioClip clip, double edge)
    {
        ArgumentNullException.ThrowIfNull(clip);
        double lowest = Math.Max(0, clip.Offset - clip.Start);
        double highest = clip.Duration - clip.TrimEnd - MinClipLength;
        double offset = Math.Max(lowest, Math.Min(clip.Offset + (edge - clip.Start), highest));
        // The sum rounds to a hair below 0 when the clamp is hit exactly; the validator would refuse it.
        return FitFades(clip with { Start = Math.Max(0, clip.Start + (offset - clip.Offset)), Offset = offset });
    }

    /// <summary>Moves the end edge to <paramref name="edge"/> on the timeline, to at most the end of the file and at least the minimum length.</summary>
    public static AudioClip TrimEnd(AudioClip clip, double edge)
    {
        ArgumentNullException.ThrowIfNull(clip);
        double wanted = clip.Duration - clip.Offset - (edge - clip.Start);
        double trim = Math.Max(0, Math.Min(wanted, clip.Duration - clip.Offset - MinClipLength));
        return FitFades(clip with { TrimEnd = trim });
    }

    /// <summary>Sets the fades as a drag does: each from 0 up to what is left of the clip after the other.</summary>
    /// <remarks>A fade not given keeps its length. When both are given the fade in is kept to the clip and the fade out to what it leaves, so the result does not depend on the old fades.</remarks>
    public static AudioClip SetFades(AudioClip clip, double? fadeIn, double? fadeOut)
    {
        ArgumentNullException.ThrowIfNull(clip);
        double visible = VisibleLength(clip);
        double otherOut = fadeIn is not null && fadeOut is not null ? 0 : clip.FadeOut;
        double inLength = fadeIn is { } wantedIn ? Math.Clamp(wantedIn, 0, Math.Max(0, visible - otherOut)) : clip.FadeIn;
        double outLength = fadeOut is { } wantedOut ? Math.Clamp(wantedOut, 0, Math.Max(0, visible - inLength)) : clip.FadeOut;
        return clip with { FadeIn = inLength, FadeOut = outLength };
    }

    /// <summary>"Foo" becomes "Foo (copy)", then "Foo (copy 2)", instead of stacking " (copy)" for ever.</summary>
    /// <remarks>A long name is cut so the copy still fits the name limit.</remarks>
    public static string NextCopyName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        Match match = CopySuffix().Match(name);
        int number = match.Success && match.Groups[2].Success ? int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture) : 1;
        string suffix = match.Success ? $" (copy {(number + 1).ToString(CultureInfo.InvariantCulture)})" : " (copy)";
        string stem = match.Success ? match.Groups[1].Value : name;
        int room = ProjectLimits.MaxName - suffix.Length;
        if (stem.Length > room)
        {
            // Never cut a surrogate pair in half: a lone half is not text a project keeps.
            stem = stem[..(char.IsHighSurrogate(stem[room - 1]) ? room - 1 : room)];
        }
        return stem + suffix;
    }

    /// <summary>The lowest-numbered name like "Clip 2" that no clip of the project uses yet.</summary>
    public static string NextName(ProjectDocument document, string prefix)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(prefix);
        HashSet<string> used = [.. document.Tracks.SelectMany(track => track.Clips.Select(clip => clip.Name).Concat(track.MidiClips.Select(clip => clip.Name)))];
        for (int number = 1; ; number++)
        {
            string name = $"{prefix} {number.ToString(CultureInfo.InvariantCulture)}";
            if (!used.Contains(name))
            {
                return name;
            }
        }
    }

    /// <summary>A clip's metadata with only the lyrics kept, which is all a copy or the second half of a split carries; null when there are none.</summary>
    public static ClipMeta? LyricsOnly(ClipMeta? meta) => meta?.Lyrics is { } lyrics ? new ClipMeta { Lyrics = lyrics } : null;

    // The fades never overlap: the fade in is kept to what the clip plays and the fade out to what it leaves.
    private static AudioClip FitFades(AudioClip clip)
    {
        double visible = VisibleLength(clip);
        double fadeIn = Math.Min(clip.FadeIn, visible);
        return clip with { FadeIn = fadeIn, FadeOut = Math.Min(clip.FadeOut, visible - fadeIn) };
    }

    [GeneratedRegex(@"^(.*) \(copy(?: (\d+))?\)$", RegexOptions.CultureInvariant)]
    private static partial Regex CopySuffix();
}
