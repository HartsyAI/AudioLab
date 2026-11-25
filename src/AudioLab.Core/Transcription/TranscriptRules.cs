using AudioLab.Core.Model;

namespace AudioLab.Core.Transcription;

/// <summary>The checks of a transcript document and of a model's answer (D94, D98). Pure: no I/O and no clock.</summary>
public static class TranscriptRules
{
    private const int MaxProblems = 100;

    /// <summary>What is wrong with a transcript document, in words a user can read; empty when it is sound.</summary>
    public static IReadOnlyList<string> DocumentProblems(TranscriptDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        List<string> problems = [];
        if (document.Version != TranscriptDocument.CurrentVersion)
        {
            problems.Add($"The transcript version {document.Version} is not one this server reads.");
        }
        if (!IdRules.IsValid(document.Media))
        {
            problems.Add("The transcript names no valid media id.");
        }
        if (!IsSha256(document.Sha256))
        {
            problems.Add("The transcript hash is not a SHA-256 in lower-case hexadecimal.");
        }
        if (string.IsNullOrWhiteSpace(document.Model) || document.Model.Length > TranscriptionLimits.MaxModelIdChars)
        {
            problems.Add($"The model name must be 1 to {TranscriptionLimits.MaxModelIdChars} characters.");
        }
        if (!IsSeconds(document.Duration))
        {
            problems.Add("The duration is not a length of seconds in range.");
            return Cap(problems);
        }
        if (document.Segments.Count > TranscriptionLimits.MaxSegments)
        {
            problems.Add($"A transcript holds at most {TranscriptionLimits.MaxSegments} segments.");
        }
        double previousStart = 0;
        for (int index = 0; index < document.Segments.Count && problems.Count < MaxProblems; index++)
        {
            TranscriptSegment segment = document.Segments[index];
            string where = $"Segment {index}";
            if (!IsSeconds(segment.Start) || !IsSeconds(segment.End) || segment.Start > segment.End
                || segment.End > document.Duration || segment.Start < previousStart)
            {
                problems.Add($"{where} must start and end in order, within 0 and the duration.");
            }
            else
            {
                previousStart = segment.Start;
            }
            AddTextProblems(problems, where, segment.Text, segment.Kind);
            if (segment.Words is { } words)
            {
                AddWordProblems(problems, where, words, segment.Start, segment.End);
            }
        }
        return Cap(problems);
    }

    /// <summary>The answer with its text and times made usable: whitespace collapsed, segments with no text dropped, each segment and each word
    /// clamped into the window (and the word into its segment), and the segments sorted by start. A value that is not finite is kept,
    /// so <see cref="AnswerProblems"/> refuses it.</summary>
    public static IReadOnlyList<SpeechSegment> Normalise(IReadOnlyList<SpeechSegment> answer, double windowLength)
    {
        ArgumentNullException.ThrowIfNull(answer);
        List<SpeechSegment> kept = [];
        foreach (SpeechSegment segment in answer)
        {
            if (!double.IsFinite(segment.Start) || !double.IsFinite(segment.End))
            {
                kept.Add(segment);
                continue;
            }
            string text = Collapse(segment.Text);
            if (text.Length == 0)
            {
                continue;
            }
            double start = Math.Clamp(segment.Start, 0, windowLength);
            double end = Math.Clamp(segment.End, start, windowLength);
            ValueList<LyricWord>? words = segment.Words is { } given
                ? new ValueList<LyricWord>(given.Select(word => ClampWord(word, start, end)))
                : null;
            kept.Add(new SpeechSegment(start, end, text, segment.Kind, words));
        }
        kept.Sort((left, right) => left.Start.CompareTo(right.Start));
        return kept;
    }

    /// <summary>What is wrong with a model's answer for one window, in words; empty when every segment is usable.</summary>
    /// <remarks>Call it on the answer after <see cref="Normalise"/>, which has put every time inside the window.</remarks>
    public static IReadOnlyList<string> AnswerProblems(IReadOnlyList<SpeechSegment> answer, double windowLength)
    {
        ArgumentNullException.ThrowIfNull(answer);
        List<string> problems = [];
        if (answer.Count > TranscriptionLimits.MaxSegmentsPerWindow)
        {
            problems.Add($"A model answer holds at most {TranscriptionLimits.MaxSegmentsPerWindow} segments for one window.");
        }
        double tolerance = TranscriptionLimits.WindowToleranceSeconds;
        for (int index = 0; index < answer.Count && problems.Count < MaxProblems; index++)
        {
            SpeechSegment segment = answer[index];
            string where = $"Segment {index} of the answer";
            if (!double.IsFinite(segment.Start) || !double.IsFinite(segment.End) || segment.Start > segment.End
                || segment.Start < -tolerance || segment.End > windowLength + tolerance)
            {
                problems.Add($"{where} lies outside its window.");
            }
            AddTextProblems(problems, where, segment.Text, segment.Kind);
            if (segment.Words is { } words)
            {
                AddWordProblems(problems, where, words, segment.Start, segment.End);
            }
        }
        return Cap(problems);
    }

    /// <summary>The segments of a window's answer that the window owns, in media seconds (D91); the rest belong to a neighbour. The answer is
    /// normalised first, so the stored segments pass <see cref="DocumentProblems"/> by construction.</summary>
    /// <returns>False, with the first problem, when the answer is not one the server accepts.</returns>
    public static bool TryOwn(
        TranscriptionWindow window,
        IReadOnlyList<SpeechSegment> answer,
        double durationSeconds,
        out ValueList<TranscriptSegment> owned,
        out string problem)
    {
        ArgumentNullException.ThrowIfNull(answer);
        IReadOnlyList<SpeechSegment> usable = Normalise(answer, window.LengthSeconds);
        IReadOnlyList<string> problems = AnswerProblems(usable, window.LengthSeconds);
        if (problems.Count > 0)
        {
            owned = new ValueList<TranscriptSegment>([]);
            problem = problems[0];
            return false;
        }
        List<TranscriptSegment> kept = [];
        foreach (SpeechSegment segment in usable)
        {
            double start = window.StartSeconds + segment.Start;
            bool isOwned = start >= window.StartSeconds
                && (start < window.OwnedEndSeconds || (window.IsLast && start <= window.OwnedEndSeconds));
            if (!isOwned || start >= durationSeconds)
            {
                continue;
            }
            double end = Math.Max(start, Math.Min(durationSeconds, window.StartSeconds + segment.End));
            ValueList<LyricWord>? words = segment.Words is { } given
                ? new ValueList<LyricWord>(given.Select(word => ClampWord(
                    word with { Start = word.Start + window.StartSeconds, End = word.End + window.StartSeconds },
                    start,
                    end)))
                : null;
            kept.Add(new TranscriptSegment(start, end, segment.Text, segment.Kind, words));
        }
        owned = new ValueList<TranscriptSegment>(kept);
        problem = string.Empty;
        return true;
    }

    private static LyricWord ClampWord(LyricWord word, double start, double end)
    {
        double wordStart = Math.Clamp(word.Start, start, end);
        return word with { Start = wordStart, End = Math.Clamp(Math.Max(word.End, wordStart), start, end) };
    }

    private static string Collapse(string? text)
    {
        return string.Join(' ', (text ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    private static void AddTextProblems(List<string> problems, string where, string text, SpeechKind kind)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length > TranscriptionLimits.MaxTextChars)
        {
            problems.Add($"{where} needs text of 1 to {TranscriptionLimits.MaxTextChars} characters.");
        }
        else if (text.Any(character => char.IsControl(character) && !char.IsWhiteSpace(character)))
        {
            problems.Add($"{where} holds a control character.");
        }
        if (!Enum.IsDefined(kind))
        {
            problems.Add($"{where} has a kind this server does not know.");
        }
    }

    private static void AddWordProblems(List<string> problems, string where, ValueList<LyricWord> words, double start, double end)
    {
        foreach (LyricWord word in words)
        {
            if (string.IsNullOrWhiteSpace(word.Word) || word.Word.Length > TranscriptionLimits.MaxWordChars
                || !IsSeconds(word.Start) || !IsSeconds(word.End) || word.Start > word.End
                || word.Start < start || word.End > end)
            {
                problems.Add($"{where} has a word that lies outside its segment.");
                return;
            }
        }
    }

    private static bool IsSha256(string value)
    {
        return value.Length == 64 && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
    }

    private static bool IsSeconds(double value)
    {
        return double.IsFinite(value) && value >= 0 && value <= ProjectLimits.MaxSeconds;
    }

    private static IReadOnlyList<string> Cap(List<string> problems)
    {
        return problems.Count <= MaxProblems ? problems : problems.Take(MaxProblems).ToList();
    }
}
