using AudioLab.Core.Api;
using AudioLab.Core.Model;
using AudioLab.Web.Services;

namespace AudioLab.Web.Tests;

/// <summary>What the user is told after a file was imported: the tempo the project did not take, and what the reader left out.</summary>
public sealed class MidiImportNoticeTests
{
    private readonly ProjectDocument _document = Song.Empty();

    private static MidiImportResult Result(double bpm, int tempoChanges = 0, int ignored = 0, int unclosed = 0)
    {
        return new MidiImportResult("clp_1", 5, 12, 0, 4, bpm, tempoChanges, ignored, unclosed);
    }

    /// <summary>A file at the project's own tempo with nothing left out has nothing to say.</summary>
    [Fact]
    public void AFileAtTheProjectsTempoWithNothingLeftOutHasNothingToSay()
    {
        Assert.Null(MidiImportNotice.Of(Result(_document.Bpm), _document, 0));
    }

    /// <summary>A file at another tempo says both tempos and that the project's was not changed.</summary>
    [Fact]
    public void AFileAtAnotherTempoSaysBothTemposAndThatTheProjectsWasNotChanged()
    {
        string? notice = MidiImportNotice.Of(Result(_document.Bpm + 20), _document, 0);

        Assert.NotNull(notice);
        Assert.Contains($"{_document.Bpm + 20:0.##} bpm", notice, StringComparison.Ordinal);
        Assert.Contains($"the project's is {_document.Bpm:0.##}", notice, StringComparison.Ordinal);
        Assert.Contains("tempo was not changed", notice, StringComparison.Ordinal);
    }

    /// <summary>Later tempo changes, note-offs that ended nothing and notes never ended are each said.</summary>
    [Fact]
    public void LaterTempoChangesAndWhatTheReaderLeftOutAreEachSaid()
    {
        string? notice = MidiImportNotice.Of(Result(_document.Bpm, tempoChanges: 3, ignored: 2, unclosed: 1), _document, 0);

        Assert.NotNull(notice);
        Assert.Contains("changes tempo 3 times", notice, StringComparison.Ordinal);
        Assert.Contains("2 note-off events had no note to end", notice, StringComparison.Ordinal);
        Assert.Contains("1 notes were never ended", notice, StringComparison.Ordinal);
        Assert.Contains("once", MidiImportNotice.Of(Result(_document.Bpm, tempoChanges: 1), _document, 0), StringComparison.Ordinal);
    }
}
