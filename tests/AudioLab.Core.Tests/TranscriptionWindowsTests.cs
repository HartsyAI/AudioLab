using AudioLab.Core.Model;
using AudioLab.Core.Transcription;

namespace AudioLab.Core.Tests;

public sealed class TranscriptionWindowsTests
{
    private const double TenHours = 36_000;

    [Fact]
    public void TenHoursIsCountedWithoutRunningAnything()
    {
        Assert.Equal(1286, TranscriptionWindows.CountOf(TenHours));
        TranscriptionWindow first = TranscriptionWindows.At(0, TenHours);
        Assert.Equal(0, first.StartSeconds);
        Assert.Equal(30, first.LengthSeconds);
        Assert.Equal(28, first.OwnedEndSeconds);
        TranscriptionWindow last = TranscriptionWindows.At(1285, TenHours);
        Assert.Equal(35_980, last.StartSeconds);
        Assert.Equal(20, last.LengthSeconds);
        Assert.Equal(TenHours, last.OwnedEndSeconds);
        Assert.True(last.IsLast);
    }

    [Fact]
    public void NeighbouringWindowsOwnContiguousRanges()
    {
        int count = TranscriptionWindows.CountOf(TenHours);
        for (int index = 0; index < count - 1; index++)
        {
            Assert.Equal(TranscriptionWindows.At(index + 1, TenHours).StartSeconds, TranscriptionWindows.At(index, TenHours).OwnedEndSeconds);
        }
    }

    [Fact]
    public void AnEmptyFileHasNoWindowsAndAShortOneHasOne()
    {
        Assert.Equal(0, TranscriptionWindows.CountOf(0));
        Assert.Equal(1, TranscriptionWindows.CountOf(1.5));
        TranscriptionWindow only = TranscriptionWindows.At(0, 1.5);
        Assert.Equal(1.5, only.LengthSeconds);
        Assert.Equal(1.5, only.OwnedEndSeconds);
    }

    [Fact]
    public void AWindowThatWouldStartPastTheFileIsRefused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TranscriptionWindows.At(1286, TenHours));
    }

    [Fact]
    public void EachSegmentIsKeptByExactlyOneWindow()
    {
        double duration = 60;
        List<TranscriptSegment> kept = [];
        int count = TranscriptionWindows.CountOf(duration);
        for (int index = 0; index < count; index++)
        {
            TranscriptionWindow window = TranscriptionWindows.At(index, duration);
            List<SpeechSegment> answer = [new SpeechSegment(1, 2, "one", SpeechKind.Speech)];
            if (window.LengthSeconds >= 28)
            {
                answer.Add(new SpeechSegment(27.5, 28.5, "straddles", SpeechKind.Singing));
            }
            Assert.True(TranscriptRules.TryOwn(window, answer, duration, out ValueList<TranscriptSegment> owned, out string problem, out _), problem);
            kept.AddRange(owned);
        }
        Assert.Equal(new double[] { 1, 27.5, 29, 55.5, 57 }, kept.Select(segment => segment.Start).ToArray());
    }

    [Fact]
    public void AControlCharacterInTheMiddleOfASegmentIsRemovedAndCounted()
    {
        TranscriptionWindow window = TranscriptionWindows.At(0, 60);
        List<SpeechSegment> answer =
        [
            new SpeechSegment(1, 2, "ab\u0000cd\u0007ef", SpeechKind.Speech),
            new SpeechSegment(3, 4, "tab\tand\nnewline", SpeechKind.Speech),
        ];

        Assert.True(
            TranscriptRules.TryOwn(window, answer, 60, out ValueList<TranscriptSegment> owned, out string problem, out int stripped),
            problem);

        Assert.Equal(2, stripped);
        Assert.Equal(new[] { "abcdef", "tab and newline" }, owned.Select(segment => segment.Text).ToArray());
    }

    [Fact]
    public void AnAnswerOutsideItsWindowIsRefused()
    {
        Assert.NotEmpty(TranscriptRules.AnswerProblems([new SpeechSegment(0, 31, "late", SpeechKind.Speech)], 30));
        Assert.NotEmpty(TranscriptRules.AnswerProblems([new SpeechSegment(double.NaN, 1, "nan", SpeechKind.Speech)], 30));
        Assert.NotEmpty(TranscriptRules.AnswerProblems([new SpeechSegment(0, 1, " ", SpeechKind.Speech)], 30));
        Assert.NotEmpty(TranscriptRules.AnswerProblems([new SpeechSegment(0, 1, "kind", (SpeechKind)99)], 30));
        Assert.NotEmpty(TranscriptRules.AnswerProblems([new SpeechSegment(2, 1, "backwards", SpeechKind.Speech)], 30));
        Assert.Empty(TranscriptRules.AnswerProblems([new SpeechSegment(0, 1, "fine", SpeechKind.Speech)], 30));
    }

    [Fact]
    public void AMalformedDocumentIsRefused()
    {
        string hash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
        string media = MediaIdFor("m1");
        TranscriptSegment late = new(0, 5, "late", SpeechKind.Speech);
        TranscriptSegment early = new(3, 4, "early", SpeechKind.Speech);
        Assert.Empty(TranscriptRules.DocumentProblems(Document(media, hash, 10, [late])));
        Assert.NotEmpty(TranscriptRules.DocumentProblems(Document(media, "NOT-A-HASH", 10, [late])));
        Assert.NotEmpty(TranscriptRules.DocumentProblems(Document(media, hash, 10, [early, late])));
        Assert.NotEmpty(TranscriptRules.DocumentProblems(Document(media, hash, 3, [late])));
        Assert.NotEmpty(TranscriptRules.DocumentProblems(Document(media, hash, 10, [new TranscriptSegment(0, 1, "x", (SpeechKind)99)])));
        Assert.NotEmpty(TranscriptRules.DocumentProblems(new TranscriptDocument(2, media, hash, "m", 10, [late])));
    }

    private static string MediaIdFor(string value) => MediaId.From(value).Value;

    private static TranscriptDocument Document(string media, string hash, double duration, IEnumerable<TranscriptSegment> segments)
    {
        return new TranscriptDocument(TranscriptDocument.CurrentVersion, media, hash, "model", duration, new ValueList<TranscriptSegment>(segments));
    }
}
