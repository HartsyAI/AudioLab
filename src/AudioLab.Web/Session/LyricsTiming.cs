using System.Globalization;
using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Web.Session;

/// <summary>The lyrics of an audio clip on the timeline: the word under the playhead, and the word-time preview that Core's own rule gives.</summary>
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

    /// <summary>The source-audio second that the timeline's position is in the clip, or null when the clip does not play at it.</summary>
    public static double? SourceAt(AudioClip clip, double timeline)
    {
        ArgumentNullException.ThrowIfNull(clip);
        return timeline >= clip.Start && timeline < ClipRules.EndOf(clip) ? timeline - clip.Start + clip.Offset : null;
    }

    /// <summary>The index of the word that is sounding at a source time, or null when none is.</summary>
    /// <remarks>Words are in order and do not overlap, so the last one that starts by the time is the only candidate.</remarks>
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

    /// <summary>What a word-time edit comes to: the word as Core would keep it, with the times set, held between its neighbours.</summary>
    /// <param name="lyrics">The lyrics as they are.</param>
    /// <param name="word">The word's index.</param>
    /// <param name="start">The wanted start, or null to keep it.</param>
    /// <param name="end">The wanted end, or null to keep it.</param>
    /// <returns>The word the server will keep, to the millisecond.</returns>
    public static LyricWord Preview(ClipLyrics lyrics, int word, double? start, double? end)
    {
        ArgumentNullException.ThrowIfNull(lyrics);
        return LyricsRules.SetWordTime(lyrics, word, start, end).Words[word];
    }

    /// <summary>Words for an edit the neighbours held, or null when the word took the time asked for.</summary>
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
        double at = heldStart ? kept.Start : kept.End;
        return string.Create(
            CultureInfo.InvariantCulture,
            $"Held by the word before or after at {at:0.000} s, so the words do not overlap.");
    }
}
