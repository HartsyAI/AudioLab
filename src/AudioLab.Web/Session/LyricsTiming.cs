using System.Globalization;
using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Web.Session;

/// <summary>The lyrics of an audio clip on the timeline: the word under the playhead, and the word-time moves the lyrics tab asks for.</summary>
public static class LyricsTiming
{
    /// <summary>How far a key press moves a word, in seconds.</summary>
    public const double Nudge = 0.05;

    /// <summary>Finds an audio clip of the project and its track; MIDI clips have no lyrics, so they are not found.</summary>
    public static (Track Track, AudioClip Clip)? Find(ProjectDocument document, ClipId clip)
    {
        ArgumentNullException.ThrowIfNull(document);
        foreach (Track track in document.Tracks)
        {
            if (track.Clips.FirstOrDefault(candidate => candidate.Id == clip) is { } found)
            {
                return (track, found);
            }
        }
        return null;
    }

    /// <summary>The source-audio second under a timeline position, or null when the clip does not play there.</summary>
    public static double? SourceAt(AudioClip clip, double timeline)
    {
        ArgumentNullException.ThrowIfNull(clip);
        return timeline >= clip.Start && timeline < ClipRules.EndOf(clip) ? timeline - clip.Start + clip.Offset : null;
    }

    /// <summary>The index of the word sounding at a source time, or null when none is.</summary>
    /// <remarks>Words are in order and do not overlap, so the last word that starts by the time is the only candidate.</remarks>
    public static int? WordAt(IReadOnlyList<LyricWord> words, double? source)
    {
        ArgumentNullException.ThrowIfNull(words);
        if (source is not { } time || words.Count == 0 || time < words[0].Start)
        {
            return null;
        }
        int low = 0;
        int high = words.Count - 1;
        while (low < high)
        {
            int middle = (low + high + 1) / 2;
            if (words[middle].Start <= time)
            {
                low = middle;
            }
            else
            {
                high = middle - 1;
            }
        }
        return time < words[low].End ? low : null;
    }

    /// <summary>The start and end a part of a word moves to, when it moves by <paramref name="seconds"/> from where it is now.</summary>
    /// <remarks>A move of the whole word is clamped as a whole, so its length never changes; a start or an end is left to Core's rule.</remarks>
    public static (double? Start, double? End) Targets(ClipLyrics lyrics, int word, LyricWordPart part, double seconds)
    {
        ArgumentNullException.ThrowIfNull(lyrics);
        LyricWord current = lyrics.Words[word];
        double by = ClampMove(lyrics, word, seconds);
        return part switch
        {
            LyricWordPart.Body => (current.Start + by, current.End + by),
            LyricWordPart.Start => (current.Start + seconds, null),
            _ => (null, current.End + seconds),
        };
    }

    /// <summary>The move of a whole word that its neighbours allow, to the millisecond.</summary>
    /// <remarks>The word may not start before the word before it ends, nor end after the word after it starts.</remarks>
    public static double ClampMove(ClipLyrics lyrics, int word, double seconds)
    {
        ArgumentNullException.ThrowIfNull(lyrics);
        LyricWord current = lyrics.Words[word];
        double low = word > 0 ? lyrics.Words[word - 1].End : 0;
        double high = word + 1 < lyrics.Words.Count ? lyrics.Words[word + 1].Start : double.PositiveInfinity;
        double by = LyricsRules.Milliseconds(seconds);
        return Math.Clamp(by, low - current.Start, high - current.End);
    }

    /// <summary>What a whole-word move came to: the text that says a neighbour held it, or null when it moved as asked.</summary>
    public static string? MoveHeldText(ClipLyrics lyrics, int word, double seconds)
    {
        return Math.Abs(ClampMove(lyrics, word, seconds) - LyricsRules.Milliseconds(seconds)) > double.Epsilon
            ? "Held by the word before or after, so the word keeps its length."
            : null;
    }

    /// <summary>What a word-time edit comes to: the word as Core keeps it, with the times set.</summary>
    /// <param name="lyrics">The lyrics as they are.</param>
    /// <param name="word">The word's index.</param>
    /// <param name="start">The wanted start, or null to keep it.</param>
    /// <param name="end">The wanted end, or null to keep it.</param>
    public static LyricWord Preview(ClipLyrics lyrics, int word, double? start, double? end)
    {
        ArgumentNullException.ThrowIfNull(lyrics);
        return LyricsRules.SetWordTime(lyrics, word, start, end).Words[word];
    }

    /// <summary>Words for a start or an end that Core held, or null when the word took the time asked for.</summary>
    public static string? HeldText(ClipLyrics lyrics, int word, double? start, double? end)
    {
        LyricWord kept = Preview(lyrics, word, start, end);
        bool heldStart = start is { } wanted && Math.Abs(kept.Start - LyricsRules.Milliseconds(wanted)) > double.Epsilon;
        bool heldEnd = end is { } wantedEnd && Math.Abs(kept.End - LyricsRules.Milliseconds(wantedEnd)) > double.Epsilon;
        if (!heldStart && !heldEnd)
        {
            return null;
        }
        if (!heldStart && end is { } asked && asked < kept.Start)
        {
            return "A word cannot end before it starts.";
        }
        if (heldStart && end is null && start is { } begins && begins > kept.End)
        {
            return "A word cannot start after it ends.";
        }
        double at = heldStart ? kept.Start : kept.End;
        return string.Create(CultureInfo.InvariantCulture, $"Held by the word before or after at {at:0.000} s, so the words do not overlap.");
    }

    /// <summary>Whether the word at <paramref name="index"/> is still the one a drag began on: the same text and the same times.</summary>
    public static bool SameWord(ClipLyrics lyrics, int index, LyricWord began)
    {
        ArgumentNullException.ThrowIfNull(lyrics);
        return index >= 0 && index < lyrics.Words.Count && lyrics.Words[index] == began;
    }
}
