using AudioLab.Core.Model;
using AudioLab.Web.Components.PianoRoll;
using AudioLab.Web.Session.Roll;
using Microsoft.Extensions.DependencyInjection;

namespace AudioLab.Web.Tests;

/// <summary>The paths the roll draws stay small when the notes are many: what the eye cannot tell apart is one rectangle.</summary>
public sealed class RollNotesPathTests
{
    private static int Rectangles(string path) => path.Count(letter => letter == 'M');

    private static MidiNote Note(int id, int pitch, double start, double duration, int velocity = 90)
    {
        return new MidiNote(new NoteId(id), pitch, start, duration, velocity);
    }

    /// <summary>Notes of one pitch that touch at the pixel are one rectangle, so ten thousand of them are two at most.</summary>
    [Fact]
    public void NotesOfOnePitchThatTouchAtThePixelAreOneRectangle()
    {
        MidiNote[] notes = [.. Enumerable.Range(1, 10_000).Select(id => Note(id, 60, id * 0.001, 0.0005))];

        string path = RollNotes.Rectangles(notes, new RollGeometry(80, 12, 20));

        Assert.True(Rectangles(path) <= 2, $"{Rectangles(path)} rectangles.");
    }

    /// <summary>Notes apart stay apart, and other pitches and other steps of velocity are their own rectangles.</summary>
    [Fact]
    public void NotesApartStayApartAndPitchesAndVelocityLevelsAreTheirOwn()
    {
        MidiNote[] notes = [Note(1, 60, 0, 0.5), Note(2, 60, 2, 0.5), Note(3, 62, 0, 0.5), Note(4, 60, 0.4, 0.5, 120)];

        string[] levels = RollNotes.ByLevel(notes, new RollGeometry(80, 12, 4));

        Assert.Equal(RollNotes.Levels, levels.Length);
        Assert.Equal(3, Rectangles(levels[RollNotes.Level(90)]));
        Assert.Equal(1, Rectangles(levels[RollNotes.Level(120)]));
        Assert.Equal(3, Rectangles(RollNotes.Rectangles(notes, new RollGeometry(80, 12, 4))));
    }

    /// <summary>A note is at least two pixels wide, and a note past the end of the clip is drawn to the end.</summary>
    [Fact]
    public void ANoteIsAtLeastTwoPixelsWideAndIsDrawnNoFurtherThanTheClip()
    {
        string tiny = RollNotes.Rectangles([Note(1, 60, 1, 0.0001)], new RollGeometry(80, 12, 4));
        string past = RollNotes.Rectangles([Note(1, 60, 3.5, 5)], new RollGeometry(80, 12, 4));

        Assert.Equal("M80 805h2v10h-2z", tiny);
        Assert.Equal("M280 805h40v10h-40z", past);
    }

    /// <summary>The velocity lane draws one bar for each pixel column, so notes that start together are one bar of the loudest.</summary>
    [Fact]
    public async Task TheVelocityLaneDrawsOneBarForEachPixelColumn()
    {
        MidiNote[] notes = [.. Enumerable.Range(1, 10_000).Select(id => Note(id, 60, id * 0.0001, 0.001, 1 + (id % 100)))];
        MidiClip clip = Song.Midi("m", 0, 20, notes);

        RenderedMarkup lane = await RenderedMarkup.OfAsync<VelocityLane>(
            new Dictionary<string, object?> { ["Clip"] = clip, ["Geometry"] = new RollGeometry(80, 12, 20), ["ColorToken"] = "var(--track-1)" },
            services => services.AddSingleton<RollDragState>());

        string bars = lane.Values("d")[0];
        Assert.True(Rectangles(bars) <= 81, $"{Rectangles(bars)} bars.");
    }
}
