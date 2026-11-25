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
                AddWordProblems(problems, where, words, segment.Start, segment.End, 0);
            }
        }
        return Cap(problems);
    }

    /// <summary>What is wrong with a model's answer for one window, in words; empty when every segment is usable.</summary>
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
            if (!IsSeconds(segment.Start) || !IsSeconds(segment.End) || segment.Start > segment.End
                || segment.Start < -tolerance || segment.End > windowLength + tolerance)
            {
                problems.Add($"{where} lies outside its window.");
            }
            AddTextProblems(problems, where, segment.Text, segment.Kind);
            if (segment.Words is { } words)
            {
                AddWordProblems(problems, where, words, segment.Start, segment.End, tolerance);
            }
        }
        return Cap(problems);
    }

    /// <summary>The segments of a window's answer that the window owns, in media seconds (D91); the rest belong to a neighbour.</summary>
    /// <exception cref="ArgumentException">The answer fails <see cref="AnswerProblems"/>.</exception>
    public static ValueList<TranscriptSegment> Own(TranscriptionWindow window, IReadOnlyList<SpeechSegment> answer, double durationSeconds)
    {
        ArgumentNullException.ThrowIfNull(answer);
        IReadOnlyList<string> problems = AnswerProblems(answer, window.LengthSeconds);
        if (problems.Count > 0)
        {
            throw new ArgumentException(problems[0], nameof(answer));
        }
        List<TranscriptSegment> kept = [];
        foreach (SpeechSegment segment in answer)
        {
            double start = Math.Max(0, window.StartSeconds + segment.Start);
            bool owned = start >= window.StartSeconds
                && (start < window.OwnedEndSeconds || (window.IsLast && start <= window.OwnedEndSeconds));
            if (!owned || start >= durationSeconds)
            {
                continue;
            }
            double end = Math.Min(durationSeconds, window.StartSeconds + segment.End);
            ValueList<LyricWord>? words = segment.Words is { } given
                ? new ValueList<LyricWord>(given.Select(word => word with
                {
                    Start = word.Start + window.StartSeconds,
                    End = word.End + window.StartSeconds,
                }))
                : null;
            kept.Add(new TranscriptSegment(start, Math.Max(start, end), segment.Text, segment.Kind, words));
        }
        return new ValueList<TranscriptSegment>(kept);
    }

    private static void AddTextProblems(List<string> problems, string where, string text, SpeechKind kind)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length > TranscriptionLimits.MaxTextChars)
        {
            problems.Add($"{where} needs text of 1 to {TranscriptionLimits.MaxTextChars} characters.");
        }
        else if (text.Any(character => char.IsControl(character) && character != ' '))
        {
            problems.Add($"{where} holds a control character.");
        }
        if (!Enum.IsDefined(kind))
        {
            problems.Add($"{where} has a kind this server does not know.");
        }
    }

    private static void AddWordProblems(List<string> problems, string where, ValueList<LyricWord> words, double start, double end, double tolerance)
    {
        foreach (LyricWord word in words)
        {
            if (string.IsNullOrWhiteSpace(word.Word) || word.Word.Length > TranscriptionLimits.MaxWordChars
                || !IsSeconds(word.Start) || !IsSeconds(word.End) || word.Start > word.End
                || word.Start < start - tolerance || word.End > end + tolerance)
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
