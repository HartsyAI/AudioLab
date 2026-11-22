using System.Globalization;
using System.Text.RegularExpressions;
using AudioLab.Core.Model;

namespace AudioLab.Core.Rules;

/// <summary>The lyrics rules of the old DAW: the text and its timed words, in the seconds of the clip's source audio.</summary>
/// <remarks>Word times are source seconds, so a clip that is moved, trimmed or split keeps valid word times.</remarks>
public static partial class LyricsRules
{
    /// <summary>A pause between transcribed words, in seconds, that starts a new line.</summary>
    public const double LinePause = 0.8;

    /// <summary>The most words on one line when a transcript is broken into lines by length.</summary>
    public const int LineWords = 8;

    /// <summary>The longest a word is stretched when timing is only estimated, in seconds.</summary>
    public const double MaxEstimatedWord = 1.2;

    /// <summary>A word shorter than this, in seconds, is collapsed: the aligner gave it a frame or two.</summary>
    public const double CollapsedWord = 0.06;

    /// <summary>This many collapsed words in a row are flagged unsure.</summary>
    public const int CollapsedRun = 3;

    /// <summary>This many words in a row packed into less than <see cref="DenseSpan"/> seconds are flagged unsure.</summary>
    public const int DenseWords = 8;

    /// <summary>The span, in seconds, that <see cref="DenseWords"/> words must not be packed into.</summary>
    public const double DenseSpan = 0.9;

    /// <summary>The source a typed text has.</summary>
    public const string ManualSource = "manual";

    /// <summary>The source of words timed by an aligner.</summary>
    public const string AlignedSource = "aligned";

    /// <summary>The source of words written from a transcript.</summary>
    public const string TranscribedSource = "stt";

    /// <summary>The source of words timed by estimate.</summary>
    public const string EstimatedSource = "estimated";

    /// <summary>The source of words read from an LRC file.</summary>
    public const string LrcSource = "lrc";

    /// <summary>The source that stays when the text of generated lyrics is edited.</summary>
    public const string GeneratedSource = "generated";

    /// <summary>How long the last line of an LRC file lasts when nothing says, in seconds.</summary>
    public const double DefaultLastLine = 4;

    /// <summary>Round to milliseconds, so stored times stay short and stable; halves go up, as the old DAW's did.</summary>
    public static double Milliseconds(double seconds) => Math.Floor((seconds * 1000) + 0.5) / 1000;

    /// <summary>The lines of a lyric text with tag lines and empty lines removed, each trimmed.</summary>
    public static IReadOnlyList<string> LinesOf(string? text)
    {
        return
        [
            .. (text ?? string.Empty).Split('\n')
                .Select(line => line.TrimEnd('\r'))
                .Where(line => line.Trim().Length > 0 && !TagLine().IsMatch(line))
                .Select(line => line.Trim()),
        ];
    }

    /// <summary>The words of a lyric text, split on white space: what an aligner sees.</summary>
    public static IReadOnlyList<string> WordsOf(string? text) => Split(string.Join(' ', LinesOf(text)));

    /// <summary>A word with its punctuation and case removed, for comparing texts.</summary>
    public static string Core(string word)
    {
        ArgumentNullException.ThrowIfNull(word);
        return NotLetterOrDigit().Replace(word.ToLowerInvariant(), string.Empty);
    }

    /// <summary>Whether any word has a time the strip can use; words flagged unsure do not count.</summary>
    public static bool IsTimed(ClipLyrics? lyrics) => lyrics is not null && lyrics.Words.Any(word => word.Unsure != true);

    /// <summary>How many words are flagged unsure, probably not sung.</summary>
    public static int UnsureCount(ClipLyrics? lyrics) => lyrics?.Words.Count(word => word.Unsure == true) ?? 0;

    /// <summary>Whether a stem name is the vocals, as the old DAW matched it: "vocal" or "vocals" in any case.</summary>
    public static bool IsVocalStem(string stem) => VocalStem().IsMatch(stem ?? string.Empty);

    /// <summary>Flags the words a forced alignment most likely invented, which the strip then leaves out.</summary>
    /// <remarks>
    /// A run of three or more collapsed words, any eight in a row packed into under 0.9 s, and the words at the edge of such a squeeze
    /// that are collapsed themselves or sit beside a collapsed one. Every earlier flag is cleared first, so the result depends on the times alone.
    /// </remarks>
    public static ValueList<LyricWord> MarkUnsure(IReadOnlyList<LyricWord> words)
    {
        ArgumentNullException.ThrowIfNull(words);
        int count = words.Count;
        bool[] unsure = new bool[count];
        bool Collapsed(int k) => k >= 0 && k < count && words[k].End - words[k].Start < CollapsedWord;
        int runStart = -1;
        for (int i = 0; i <= count; i++)
        {
            bool collapsed = Collapsed(i);
            if (collapsed && runStart < 0)
            {
                runStart = i;
            }
            else if (!collapsed && runStart >= 0)
            {
                if (i - runStart >= CollapsedRun)
                {
                    Array.Fill(unsure, true, runStart, i - runStart);
                }
                runStart = -1;
            }
        }
        for (int i = 0; i + DenseWords <= count; i++)
        {
            if (words[i + DenseWords - 1].End - words[i].Start < DenseSpan)
            {
                Array.Fill(unsure, true, i, DenseWords);
            }
        }
        // A squeeze rarely ends cleanly: the flag is carried to its end without eating real words that merely follow it.
        for (int i = 1; i < count; i++)
        {
            if (!unsure[i] && unsure[i - 1] && (Collapsed(i) || Collapsed(i + 1)))
            {
                unsure[i] = true;
            }
        }
        for (int i = count - 2; i >= 0; i--)
        {
            if (!unsure[i] && unsure[i + 1] && (Collapsed(i) || Collapsed(i - 1)))
            {
                unsure[i] = true;
            }
        }
        return [.. words.Select((word, i) => word with { Unsure = unsure[i] ? true : null })];
    }

    /// <summary>Gives each word the index of the line it sits on.</summary>
    /// <remarks>
    /// A word is looked up in the text after the previous one, so a repeated word lands where it was written; case is ignored,
    /// and a word that cannot be found keeps the previous word's line.
    /// </remarks>
    public static ValueList<LyricWord> AssignLines(IReadOnlyList<LyricWord> words, IReadOnlyList<string> lines)
    {
        ArgumentNullException.ThrowIfNull(words);
        ArgumentNullException.ThrowIfNull(lines);
        string text = string.Join('\n', lines).ToLowerInvariant();
        List<int> starts = [];
        int at = 0;
        foreach (string line in lines)
        {
            starts.Add(at);
            at += line.Length + 1;
        }
        int pointer = 0;
        int last = 0;
        List<LyricWord> result = [];
        foreach (LyricWord word in words)
        {
            int found = text.IndexOf(word.Word.ToLowerInvariant(), pointer, StringComparison.Ordinal);
            if (found >= 0)
            {
                // The search only moves forward, so the line does too.
                int line = last;
                while (line + 1 < starts.Count && starts[line + 1] <= found)
                {
                    line++;
                }
                last = line;
                pointer = found + word.Word.Length;
            }
            result.Add(word with { Line = last });
        }
        return [.. result];
    }

    /// <summary>The lyrics of known text timed by an aligner, whose times count from the start of the audio it was sent.</summary>
    /// <remarks>
    /// <paramref name="offset"/> is where that audio began in the clip's source.
    /// Words that are blank or have a time that is not a number are left out.
    /// </remarks>
    public static ClipLyrics FromAlignment(string text, IEnumerable<TimedWord> heard, double offset, string? source = null, string? model = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        ValueList<LyricWord> words = AssignLines(Place(heard, offset), LinesOf(text));
        return Make(text, MarkUnsure(words), source ?? AlignedSource, model);
    }

    /// <summary>The lyrics of a transcript that has no line breaks: a new line starts after a pause of 0.8 s or after eight words.</summary>
    /// <remarks>The text is rebuilt from the lines.</remarks>
    public static ClipLyrics FromTranscript(IEnumerable<TimedWord> heard, double offset, string? source = null, string? model = null)
    {
        ArgumentNullException.ThrowIfNull(heard);
        List<LyricWord> words = [];
        int line = 0;
        int inLine = 0;
        foreach (LyricWord word in Place(heard, offset))
        {
            double gap = words.Count > 0 ? word.Start - words[^1].End : 0;
            if (words.Count > 0 && (gap > LinePause || inLine >= LineWords))
            {
                line++;
                inLine = 0;
            }
            words.Add(word with { Line = line });
            inLine++;
        }
        string text = string.Join('\n', words.GroupBy(word => word.Line).Select(group => string.Join(' ', group.Select(word => word.Word))));
        return Make(text, [.. words], source ?? TranscribedSource, model);
    }

    /// <summary>Words timed by estimate: each line gets an equal share of the span and its words split it by length.</summary>
    /// <remarks>
    /// A line takes the time until the next line instead when <paramref name="lineStarts"/> has it.
    /// Times are in the seconds of <paramref name="start"/>.
    /// </remarks>
    public static ValueList<LyricWord> Estimate(string? text, double start, double end, IReadOnlyList<double>? lineStarts = null)
    {
        IReadOnlyList<string> lines = LinesOf(text);
        List<LyricWord> words = [];
        if (lines.Count == 0 || !(end > start))
        {
            return [];
        }
        for (int i = 0; i < lines.Count; i++)
        {
            double lineStart = lineStarts is not null && i < lineStarts.Count ? lineStarts[i] : start + ((end - start) * i / lines.Count);
            double lineEnd = lineStarts is not null && i + 1 < lineStarts.Count
                ? lineStarts[i + 1]
                : lineStarts is not null ? end : start + ((end - start) * (i + 1) / lines.Count);
            IReadOnlyList<string> parts = Split(lines[i]);
            double weight = parts.Sum(part => part.Length + 1);
            double span = Math.Max(0.01, Math.Min(lineEnd - lineStart, parts.Count * MaxEstimatedWord));
            double at = lineStart;
            foreach (string part in parts)
            {
                double length = span * (part.Length + 1) / weight;
                words.Add(new LyricWord(part, Milliseconds(at), Milliseconds(at + length), i));
                at += length;
            }
        }
        return [.. words];
    }

    /// <summary>The lyrics with a new text.</summary>
    /// <remarks>The word times stay when only spacing, case, punctuation or tags changed, and are dropped otherwise.</remarks>
    public static ClipLyrics SetText(ClipLyrics lyrics, string text)
    {
        ArgumentNullException.ThrowIfNull(lyrics);
        ArgumentNullException.ThrowIfNull(text);
        IReadOnlyList<string> parts = WordsOf(text);
        bool same = lyrics.Words.Count == parts.Count && lyrics.Words.Select((word, i) => Core(word.Word) == Core(parts[i])).All(match => match);
        if (!same)
        {
            return lyrics with { Text = text, Words = [], Source = lyrics.Source == GeneratedSource ? GeneratedSource : ManualSource };
        }
        ValueList<LyricWord> renamed = [.. lyrics.Words.Select((word, i) => word with { Word = parts[i] })];
        return lyrics with { Text = text, Words = AssignLines(renamed, LinesOf(text)) };
    }

    /// <summary>Every word moved by <paramref name="seconds"/>, never before 0.</summary>
    /// <remarks>The earliest word stops at 0 and the rest keep their distance.</remarks>
    public static ClipLyrics Shift(ClipLyrics lyrics, double seconds)
    {
        ArgumentNullException.ThrowIfNull(lyrics);
        if (lyrics.Words.Count == 0)
        {
            return lyrics;
        }
        double by = Math.Max(seconds, -lyrics.Words.Min(word => word.Start));
        LyricWord Move(LyricWord word) => word with { Start = Milliseconds(word.Start + by), End = Milliseconds(word.End + by) };
        ValueList<LyricWord> moved = [.. lyrics.Words.Select(Move)];
        return lyrics with { Words = moved };
    }

    /// <summary>The lyrics for audio that starts <paramref name="seconds"/> away in the source.</summary>
    /// <remarks>
    /// Every time moves, words that end before the new start are dropped and one that straddles it is cut to start at 0. A stem cut
    /// from a clip's visible part has the source's lyrics rebased by minus the clip's offset.
    /// </remarks>
    public static ClipLyrics Rebase(ClipLyrics lyrics, double seconds)
    {
        ArgumentNullException.ThrowIfNull(lyrics);
        List<LyricWord> words = [];
        foreach (LyricWord word in lyrics.Words)
        {
            double end = Milliseconds(word.End + seconds);
            if (end > 0)
            {
                words.Add(word with { Start = Math.Max(0, Milliseconds(word.Start + seconds)), End = end });
            }
        }
        return lyrics with { Words = [.. words] };
    }

    /// <summary>The lyrics with one word's start or end changed; null keeps it.</summary>
    /// <remarks>
    /// The start stays no later than the end and the word stays out of its neighbours. Its unsure flag is cleared:
    /// a time set by hand is a time the user stands behind.
    /// </remarks>
    public static ClipLyrics SetWordTime(ClipLyrics lyrics, int index, double? start, double? end)
    {
        ArgumentNullException.ThrowIfNull(lyrics);
        LyricWord word = lyrics.Words[index];
        double low = index > 0 ? lyrics.Words[index - 1].Start : 0;
        double high = index + 1 < lyrics.Words.Count ? lyrics.Words[index + 1].End : double.PositiveInfinity;
        double s = start is { } a ? Milliseconds(Math.Min(Math.Max(a, low), word.End)) : word.Start;
        double e = end is { } b ? Milliseconds(Math.Max(Math.Min(b, high), s)) : word.End;
        LyricWord set = word with { Start = s, End = e, Unsure = null };
        return lyrics with { Words = [.. lyrics.Words.Select((candidate, i) => i == index ? set : candidate)] };
    }

    /// <summary>The lines of an LRC file with their times in seconds, by time: metadata tags are skipped and word marks are removed.</summary>
    public static IReadOnlyList<LrcLine> ParseLrc(string? text)
    {
        List<LrcLine> lines = [];
        foreach (string raw in (text ?? string.Empty).Split('\n'))
        {
            List<double> times = [];
            string rest = raw.TrimEnd('\r');
            for (Match stamp = Timestamp().Match(rest); stamp.Success; stamp = Timestamp().Match(rest))
            {
                string seconds = stamp.Groups[2].Value.Replace(':', '.');
                double minutes = double.Parse(stamp.Groups[1].Value, CultureInfo.InvariantCulture);
                times.Add((minutes * 60) + double.Parse(seconds, CultureInfo.InvariantCulture));
                rest = rest[stamp.Length..];
            }
            string body = WordMark().Replace(rest, string.Empty).Trim();
            if (times.Count > 0 && body.Length > 0)
            {
                lines.AddRange(times.Select(time => new LrcLine(time, body)));
            }
        }
        return [.. lines.OrderBy(line => line.Time)];
    }

    /// <summary>Lyrics from an LRC file, timed by estimate: each line runs to the next line's start.</summary>
    /// <remarks>
    /// The last line runs for <paramref name="lastLine"/> seconds. <paramref name="toSource"/> turns a timeline second into a source second.
    /// Null when the file has no timed line.
    /// </remarks>
    public static ClipLyrics? FromLrc(string? lrc, Func<double, double> toSource, double lastLine = DefaultLastLine)
    {
        ArgumentNullException.ThrowIfNull(toSource);
        // A bare section tag is not a sung line; keeping it would shift every later line's time.
        List<LrcLine> entries = [.. ParseLrc(lrc).Where(line => !TagLine().IsMatch(line.Text))];
        if (entries.Count == 0)
        {
            return null;
        }
        string text = string.Join('\n', entries.Select(line => line.Text));
        double[] starts = [.. entries.Select(line => toSource(line.Time))];
        // Lines that start before the clip's source begins have no audio to sit on.
        ValueList<LyricWord> words =
        [
            .. Estimate(text, starts[0], starts[^1] + lastLine, starts)
                .Where(word => word.End > 0)
                .Select(word => word with { Start = Math.Max(0, word.Start) }),
        ];
        return Make(text, words, LrcSource, null);
    }

    private static ClipLyrics Make(string text, ValueList<LyricWord> words, string source, string? model)
    {
        return new ClipLyrics { Text = text, Words = words, Source = source, Model = string.IsNullOrEmpty(model) ? null : model };
    }

    private static ValueList<LyricWord> Place(IEnumerable<TimedWord> heard, double offset)
    {
        ArgumentNullException.ThrowIfNull(heard);
        List<LyricWord> words = [];
        foreach (TimedWord item in heard)
        {
            string word = (item.Word ?? string.Empty).Trim();
            if (word.Length == 0 || !double.IsFinite(item.Start) || !double.IsFinite(item.End))
            {
                continue;
            }
            words.Add(new LyricWord(word, Milliseconds(item.Start + offset), Milliseconds(Math.Max(item.End, item.Start) + offset), 0));
        }
        return [.. words];
    }

    private static string[] Split(string text) => text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

    [GeneratedRegex(@"^\s*\[[^\]]*\]\s*$")]
    private static partial Regex TagLine();

    [GeneratedRegex(@"[^\p{L}\p{N}]+")]
    private static partial Regex NotLetterOrDigit();

    [GeneratedRegex(@"^vocals?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex VocalStem();

    [GeneratedRegex(@"^\s*\[([0-9]+):([0-9]{1,2}(?:[.:][0-9]{1,3})?)\]")]
    private static partial Regex Timestamp();

    [GeneratedRegex(@"<[0-9]+:[0-9]{1,2}(?:[.:][0-9]{1,3})?>")]
    private static partial Regex WordMark();
}
