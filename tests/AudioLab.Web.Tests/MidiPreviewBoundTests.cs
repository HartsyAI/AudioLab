using AudioLab.Core.Model;
using AudioLab.Web.Session;

namespace AudioLab.Web.Tests;

/// <summary>The preview of a MIDI clip costs a bounded path and a bounded number of steps, whatever it holds.</summary>
public sealed class MidiPreviewBoundTests
{
    private const int MillionNotes = 1_000_000;

    private const int GridCells = MidiPreview.Width + 1;

    /// <summary>A clip of a million notes gives a path under a megabyte, in steps that grow with the notes and not with their width.</summary>
    [Fact]
    public void AClipOfAMillionNotesGivesABoundedPathInABoundedNumberOfSteps()
    {
        Random random = new(11);
        MidiNote[] notes =
        [
            .. Enumerable.Range(0, MillionNotes)
                .Select(index => new MidiNote(
                    new NoteId(index + 1), random.Next(24, 100), random.NextDouble() * 99, 0.5 + random.NextDouble() * 20, 90)),
        ];
        MidiClip clip = Song.Midi("big", 0, 100, notes);

        (string path, long work) = MidiPreview.Build(clip);

        Assert.True(path.Length <= 1_000_000, $"The path is {path.Length} characters.");
        Assert.True(work <= MillionNotes + (128L * GridCells), $"The work is {work} steps.");
        Assert.Equal(MillionNotes + (76L * GridCells), work);
    }

    /// <summary>The worst path is a line in every other unit of every pitch, and is still under a megabyte.</summary>
    [Fact]
    public void TheWorstPathIsALineInEveryOtherUnitOfEveryPitchAndIsUnderAMegabyte()
    {
        List<MidiNote> notes = [];
        for (int pitch = 0; pitch < 128; pitch++)
        {
            for (int unit = 0; unit < MidiPreview.Width; unit += 2)
            {
                notes.Add(new MidiNote(new NoteId(notes.Count + 1), pitch, unit, 0.5, 90));
            }
        }

        string path = MidiPreview.Path(Song.Midi("worst", 0, 1000, [.. notes]));

        Assert.Equal(128 * 500, path.Count(letter => letter == 'M'));
        Assert.True(path.Length < 1_000_000, $"The path is {path.Length} characters.");
    }

    /// <summary>Notes of one pitch that touch or overlap are one line, so a long run of them stays small.</summary>
    [Fact]
    public void NotesOfOnePitchThatTouchAreOneLine()
    {
        MidiClip clip = Song.Midi(
            "run",
            0,
            10,
            new MidiNote(new NoteId(1), 60, 0, 1, 90),
            new MidiNote(new NoteId(2), 60, 1, 1, 90),
            new MidiNote(new NoteId(3), 60, 1.5, 3, 90),
            new MidiNote(new NoteId(4), 60, 8, 1, 90));

        Assert.Equal("M0 50h450M800 50h100", MidiPreview.Path(clip));
    }
}
