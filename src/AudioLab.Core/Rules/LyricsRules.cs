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

    /// <summary>How many character comparisons <see cref="AssignLines"/> may make in all, which no honest answer comes near.</summary>
    public const long SearchBudget = 1_000_000_000;

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
    /// and a word that cannot be found keeps the previous word's line. Every look is charged to <paramref name="budget"/>
    /// at its length times the characters it may compare; when it is spent the rest keep the line they are on.
    /// </remarks>
    public static ValueList<LyricWord> AssignLines(IReadOnlyList<LyricWord> words, IReadOnlyList<string> lines, long budget = SearchBudget)
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
        HashSet<string> missing = [];
        List<LyricWord> result = [];
        foreach (LyricWord word in words)
        {
            string wanted = word.Word.ToLowerInvariant();
            // A word that was not found after an earlier point is not found after a later one, and a hostile answer must not make
            // every word scan the whole text: once the budget is spent, the rest keep the line they are on.
            long weight = Math.Max(1, wanted.Length);
            bool known = missing.Contains(wanted);
            int found = budget > 0 && !known ? text.AsSpan(pointer).IndexOf(wanted, StringComparison.Ordinal) : -1;
            if (found >= 0)
            {
                found += pointer;
                budget -= (found - pointer + wanted.Length) * weight;
                // The search only moves forward, so the line does too.
                int line = last;
                while (line + 1 < starts.Count && starts[line + 1] <= found)
                {
                    line++;
                }
                last = line;
                pointer = found + wanted.Length;
            }
            else if (budget > 0 && !known)
            {
                budget -= (text.Length - pointer) * weight;
                missing.Add(wanted);
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
    /// <remarks>
    /// The word times stay when the words are the same as far as spacing, case, punctuation and tags go, whether compared with the old
    /// text or with the timed words, and are dropped otherwise; see <see cref="ClearTiming"/> for what dropping them leaves.
    /// </remarks>
    public static ClipLyrics SetText(ClipLyrics lyrics, string text)
    {
        ArgumentNullException.ThrowIfNull(lyrics);
        ArgumentNullException.ThrowIfNull(text);
        IReadOnlyList<string> parts = WordsOf(text);
        string[] cores = [.. parts.Select(Core)];
        bool sameText = WordsOf(lyrics.Text).Select(Core).SequenceEqual(cores);
        bool sameWords = lyrics.Words.Count == parts.Count && lyrics.Words.Select(word => Core(word.Word)).SequenceEqual(cores);
        if (!sameText && !sameWords)
        {
            return DropTiming(lyrics, text);
        }
        ValueList<LyricWord> kept = sameWords ? [.. lyrics.Words.Select((word, i) => word with { Word = parts[i] })] : lyrics.Words;
        return lyrics with { Text = text, Words = AssignLines(kept, LinesOf(text)) };
    }

    /// <summary>The lyrics with their word times dropped and the text kept; lyrics with no times are returned as they were.</summary>
    /// <remarks>
    /// What described the timing goes with it: the source becomes manual (generated lyrics stay generated and keep their model)
    /// and the unknown fields are cleared.
    /// </remarks>
    public static ClipLyrics ClearTiming(ClipLyrics lyrics)
    {
        ArgumentNullException.ThrowIfNull(lyrics);
        return lyrics.Words.Count == 0 ? lyrics : DropTiming(lyrics, lyrics.Text);
    }

    private static ClipLyrics DropTiming(ClipLyrics lyrics, string text)
    {
        bool generated = lyrics.Source == GeneratedSource;
        return lyrics with
        {
            Text = text,
            Words = [],
            Source = generated ? GeneratedSource : ManualSource,
            Model = generated ? lyrics.Model : null,
            Extra = ExtraFields.Empty,
        };
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
        if (by == 0)
        {
            return lyrics;
        }
        LyricWord Move(LyricWord word) => word with { Start = Milliseconds(word.Start + by), End = Milliseconds(word.End + by) };
        ValueList<LyricWord> moved = [.. lyrics.Words.Select(Move)];
        return lyrics with { Words = moved };
    }

    /// <summary>The lyrics for audio that starts <paramref name="seconds"/> away in the source.</summary>
    /// <remarks>
    /// Every time moves, words that end before the new start are dropped and one that straddles it is cut to start at 0. A stem cut
    /// from a clip's visible part has the source's lyrics rebased by minus the offset the stem was cut at.
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
    /// The word stays between the end of the word before it and the start of the word after it, and its end stays no earlier than its
    /// start. With both given the start is placed first and the end after it, so the result does not depend on where the word was.
    /// Its unsure flag is cleared: a time set by hand is a time the user stands behind.
    /// </remarks>
    public static ClipLyrics SetWordTime(ClipLyrics lyrics, int index, double? start, double? end)
    {
        ArgumentNullException.ThrowIfNull(lyrics);
        LyricWord word = lyrics.Words[index];
        double low = index > 0 ? lyrics.Words[index - 1].End : 0;
        double high = index + 1 < lyrics.Words.Count ? lyrics.Words[index + 1].Start : double.PositiveInfinity;
        double s = word.Start;
        double e = word.End;
        if (start is { } wantedStart)
        {
            s = Milliseconds(Between(wantedStart, low, end is null ? Math.Max(low, word.End) : high));
            e = Math.Max(e, s);
        }
        if (end is { } wantedEnd)
        {
            e = Milliseconds(Between(wantedEnd, s, Math.Max(s, high)));
        }
        LyricWord set = word with { Start = s, End = Math.Max(e, s), Unsure = null };
        return lyrics with { Words = [.. lyrics.Words.Select((candidate, i) => i == index ? set : candidate)] };
    }

    private static double Between(double value, double low, double high) => Math.Max(low, Math.Min(value, high));

    /// <summary>The lines of an LRC file with their times in seconds, by time: metadata tags are skipped and word marks are removed.</summary>
    /// <remarks>
    /// A line with several stamps gives a line per stamp, so the result is cut short and <paramref name="tooLarge"/> is true when
    /// its text would pass what a clip may hold (<see cref="ProjectLimits.MaxLongText"/> characters, <see cref="ProjectLimits.MaxListItems"/> lines).
    /// </remarks>
    public static IReadOnlyList<LrcLine> ParseLrc(string? text, out bool tooLarge)
    {
        List<LrcLine> lines = [];
        long characters = 0;
        tooLarge = false;
        // A byte order mark in front of the first stamp is not part of the file.
        foreach (string raw in (text ?? string.Empty).TrimStart('\uFEFF').Split('\n'))
        {
            string line = raw.TrimEnd('\r');
            List<double> times = [];
            int at = 0;
            for (Match stamp = Timestamp().Match(line, at); stamp.Success; stamp = Timestamp().Match(line, at))
            {
                double minutes = double.Parse(stamp.Groups[1].Value, CultureInfo.InvariantCulture);
                double time = (minutes * 60) + double.Parse(stamp.Groups[2].Value.Replace(':', '.'), CultureInfo.InvariantCulture);
                if (double.IsFinite(time))
                {
                    times.Add(time);
                }
                at = stamp.Index + stamp.Length;
            }
            string body = times.Count == 0 ? string.Empty : WordMark().Replace(line[at..], string.Empty).Trim();
            if (body.Length == 0)
            {
                continue;
            }
            characters += (long)times.Count * (body.Length + 1);
            if (characters > ProjectLimits.MaxLongText || lines.Count + (long)times.Count > ProjectLimits.MaxListItems)
            {
                tooLarge = true;
                return [];
            }
            lines.AddRange(times.Select(time => new LrcLine(time, body)));
        }
        return [.. lines.OrderBy(line => line.Time)];
    }

    /// <summary>Lyrics from the lines of an LRC file, timed by estimate: each line runs to the next line's start.</summary>
    /// <remarks>
    /// The last line runs for <paramref name="lastLine"/> seconds. <paramref name="toSource"/> turns a timeline second into a source second.
    /// Null when no line is a sung one, and lyrics with no words when every line falls before the start of the source.
    /// </remarks>
    public static ClipLyrics? FromLrc(IReadOnlyList<LrcLine> parsed, Func<double, double> toSource, double lastLine = DefaultLastLine)
    {
        ArgumentNullException.ThrowIfNull(parsed);
        ArgumentNullException.ThrowIfNull(toSource);
        // A bare section tag is not a sung line; keeping it would shift every later line's time.
        List<LrcLine> entries = [.. parsed.Where(line => !TagLine().IsMatch(line.Text))];
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

    [GeneratedRegex(@"\G\s*\[([0-9]+):([0-9]{1,2}(?:[.:][0-9]{1,3})?)\]")]
    private static partial Regex Timestamp();

    [GeneratedRegex(@"<[0-9]+:[0-9]{1,2}(?:[.:][0-9]{1,3})?>")]
    private static partial Regex WordMark();
}
