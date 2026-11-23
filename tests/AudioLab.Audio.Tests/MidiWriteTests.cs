using AudioLab.Audio.Midi;
using AudioLab.Core.Model;

namespace AudioLab.Audio.Tests;

public sealed class MidiWriteTests
{
    private static readonly TimeSignature _fourFour = new(4, 4);

    private static MidiNote Note(int id, int pitch, double start, double duration, int velocity = 100) =>
        new(new NoteId(id), pitch, start, duration, velocity);

    private static MidiClip Clip(double start, double length, MidiNote[] notes, params MidiControlChange[] controls) => new()
    {
        Id = ClipId.From("clp-written"),
        Name = "Written",
        Start = start,
        Length = length,
        NextNoteId = new NoteId(notes.Length + 1),
        Notes = new ValueList<MidiNote>(notes),
        ControlChanges = new ValueList<MidiControlChange>(controls),
        Muted = false,
    };

    private static TempoChange Change(string id, double time, double bpm) => new(TempoChangeId.From(id), time, bpm);

    private static byte[] Write(MidiClip clip, double bpm = 120, params TempoChange[] changes) => MidiFile.Write(clip, bpm, changes, _fourFour);

    [Fact]
    public void TheFileIsFormat0WithOneTrackOf480TicksAndEndsWithEndOfTrack()
    {
        byte[] file = Write(Clip(0, 1, [Note(1, 60, 0, 0.5)]));

        Assert.Equal("MThd"u8.ToArray(), file[..4]);
        Assert.Equal(new byte[] { 0, 0, 0, 6, 0, 0, 0, 1, 0x01, 0xE0 }, file[4..14]);
        Assert.Equal("MTrk"u8.ToArray(), file[14..18]);
        Assert.Equal(new byte[] { 0xFF, 0x2F, 0x00 }, file[^3..]);
    }

    [Fact]
    public void NotesControllersLengthTempoAndSignatureComeBackFromAFileThatWasWritten()
    {
        MidiClip clip = Clip(
            2,
            4,
            [Note(1, 60, 0, 0.5, 100), Note(2, 64, 0.5, 1.5, 70), Note(3, 67, 2, 1, 1)],
            new MidiControlChange(0.25, 64, 127),
            new MidiControlChange(1.5, 64, 0),
            new MidiControlChange(1.5, 7, 90));

        MidiImport import = MidiFile.Read(MidiFile.Write(clip, 90, [], new TimeSignature(6, 8)));

        Assert.Equal(90, import.Bpm, 3);
        Assert.Equal(new TimeSignature(6, 8), import.TimeSignature);
        Assert.Equal(4.0, import.Length, 3);
        Assert.Equal([(60, 100), (64, 70), (67, 1)], import.Notes.Select(note => (note.Pitch, note.Velocity)));
        Assert.All(clip.Notes.Zip(import.Notes), pair =>
        {
            Assert.Equal(pair.First.Start, pair.Second.Start, 3);
            Assert.Equal(pair.First.Duration, pair.Second.Duration, 3);
        });
        Assert.Equal(
            [(64, 127), (7, 90), (64, 0)],
            import.ControlChanges.Select(change => (change.Controller, change.Value)));
    }

    // Audit row 37: the export wrote notes and pedal events past the clip's end.
    [Fact]
    public void ANoteThatRingsPastTheClipIsCutAtItsEndAndControllersAfterItAreLeftOut()
    {
        MidiClip clip = Clip(0, 2, [Note(1, 60, 1.5, 3)], new MidiControlChange(1, 1, 50), new MidiControlChange(2.5, 1, 60));

        MidiImport import = MidiFile.Read(Write(clip));

        Assert.Equal(0.5, Assert.Single(import.Notes).Duration, 3);
        Assert.Equal(50, Assert.Single(import.ControlChanges).Value);
        Assert.Equal(2.0, import.Length, 3);
    }

    // Audit row 75: the export dropped the sustain of a pedal still down at the clip end.
    [Fact]
    public void APedalStillDownAtTheClipEndIsLetUpThere()
    {
        MidiClip clip = Clip(0, 2, [Note(1, 60, 0.5, 0.5)], new MidiControlChange(0.5, 64, 127));

        MidiImport import = MidiFile.Read(Write(clip));

        Assert.Equal([new MidiControlChange(0.5, 64, 127), new MidiControlChange(2.0, 64, 0)], import.ControlChanges.Select(Rounded));
    }

    [Fact]
    public void APedalThatWasLetUpIsNotLetUpAgainAndAnotherControllerIsNeverReleased()
    {
        MidiClip clip = Clip(
            0,
            2,
            [Note(1, 60, 0.5, 0.5)],
            new MidiControlChange(0.5, 64, 127),
            new MidiControlChange(1, 64, 0),
            new MidiControlChange(1.2, 1, 99));

        MidiImport import = MidiFile.Read(Write(clip));

        Assert.Equal(3, import.ControlChanges.Count);
    }

    [Fact]
    public void TheFileStartsWhereTheClipDoesSoItOpensAtTheTempoThatHoldsThereAndKeepsLaterChanges()
    {
        TempoChange slower = Change("tmp-a", 4, 60);
        MidiClip clip = Clip(3, 4, [Note(1, 60, 2, 1)]);

        MidiImport import = MidiFile.Read(Write(clip, 120, slower));

        Assert.Equal(120, import.Bpm, 3);
        MidiTempoEvent change = Assert.Single(import.TempoChanges);
        Assert.Equal(1.0, change.Time, 3);
        Assert.Equal(60, change.Bpm, 3);
        Assert.Equal(2.0, Assert.Single(import.Notes).Start, 3);
    }

    [Fact]
    public void AClipAfterATempoChangeOpensAtThatTempoWithNoChanges()
    {
        MidiImport import = MidiFile.Read(Write(Clip(5, 2, [Note(1, 60, 0.5, 0.5)]), 120, Change("tmp-a", 4, 60)));

        Assert.Equal(60, import.Bpm, 3);
        Assert.Empty(import.TempoChanges);
        Assert.Equal(0.5, Assert.Single(import.Notes).Start, 3);
    }

    [Fact]
    public void ATempoMapRoundTripsTheTimesOfNotesWithinHalfATick()
    {
        MidiClip clip = Clip(0, 10, [Note(1, 60, 0.3, 0.2), Note(2, 62, 3.7, 1.1), Note(3, 64, 9.1, 0.4)]);
        TempoChange[] changes = [Change("tmp-a", 2, 90), Change("tmp-b", 5.5, 170), Change("tmp-c", 8, 61)];

        MidiImport import = MidiFile.Read(Write(clip, 130, changes));

        Assert.Equal(3, import.TempoChanges.Count);
        Assert.All(clip.Notes.Zip(import.Notes), pair =>
        {
            Assert.InRange(Math.Abs(pair.First.Start - pair.Second.Start), 0, 0.0011);
            Assert.InRange(Math.Abs(pair.First.Duration - pair.Second.Duration), 0, 0.0021);
        });
    }

    [Fact]
    public void AVelocityOfZeroIsWrittenAsOneBecauseZeroWouldEndTheNote()
    {
        MidiImport import = MidiFile.Read(Write(Clip(0, 1, [Note(1, 60, 0, 0.5, velocity: 0)])));

        Assert.Equal(1, Assert.Single(import.Notes).Velocity);
    }

    [Fact]
    public void ANoteEndingWhereAnotherOfItsPitchStartsIsNotRunTogether()
    {
        MidiImport import = MidiFile.Read(Write(Clip(0, 2, [Note(1, 60, 0, 0.5), Note(2, 60, 0.5, 0.5)])));

        Assert.Equal(2, import.Notes.Count);
        Assert.All(import.Notes, note => Assert.Equal(0.5, note.Duration, 3));
    }

    [Fact]
    public void ATempoThatAFileCannotHoldIsRefusedAndTheEdgesAreWritten()
    {
        MidiClip clip = Clip(0, 1, [Note(1, 60, 0, 0.5)]);

        Assert.Throws<MidiFormatException>(() => Write(clip, 2));
        Assert.Throws<MidiFormatException>(() => Write(clip, 120, Change("tmp-a", 0.5, 3)));
        Assert.Equal(1000, MidiFile.Read(Write(clip, 1000)).Bpm, 3);
        Assert.Equal(3.58, MidiFile.Read(Write(clip, 3.58)).Bpm, 2);
    }

    [Fact]
    public void ABadClipOrSignatureIsAnArgumentError()
    {
        Assert.Throws<ArgumentException>(() => Write(Clip(0, 1, [Note(1, 128, 0, 0.5)])));
        Assert.Throws<ArgumentException>(() => Write(Clip(0, 1, [Note(1, 60, 0, 0.5)], new MidiControlChange(0, 64, 200))));
        Assert.Throws<ArgumentException>(() => Write(Clip(0, 0, [])));
        Assert.Throws<ArgumentException>(() => MidiFile.Write(Clip(0, 1, []), 120, [], new TimeSignature(7, 3)));
        Assert.Throws<ArgumentException>(() => MidiFile.Write(Clip(0, 1, []), 120, [], new TimeSignature(65, 4)));
    }

    [Fact]
    public void TheSameClipGivesTheSameBytes()
    {
        MidiClip clip = Clip(1, 3, [Note(1, 60, 0, 1), Note(2, 60, 0, 1), Note(3, 59, 0, 1)], new MidiControlChange(0.5, 64, 127));

        Assert.Equal(Write(clip, 100), Write(clip, 100));
    }

    [Fact]
    public void AClipWithNoNotesStillWritesAFileThatReadsBackWithItsLength()
    {
        MidiImport import = MidiFile.Read(Write(Clip(0, 3, [])));

        Assert.Empty(import.Notes);
        Assert.Equal(3.0, import.Length, 3);
    }

    // Audit row 47: a take or import with about 125,000 notes threw.
    [Fact]
    public void AClipOf130000NotesWritesAndReadsBack()
    {
        MidiNote[] notes = new MidiNote[130_000];
        for (int i = 0; i < notes.Length; i++)
        {
            notes[i] = Note(i + 1, 20 + (i % 100), i * 0.01, 0.2, 1 + (i % 127));
        }
        MidiClip clip = Clip(0, 1301, notes);

        MidiImport import = MidiFile.Read(Write(clip));

        Assert.Equal(130_000, import.Notes.Count);
        Assert.Equal(notes.Select(note => note.Pitch).Order(), import.Notes.Select(note => note.Pitch).Order());
    }

    // Random clips of notes on distinct pitches, so that no two notes of a pitch can swap their ends.
    [Fact]
    public void RandomClipsRoundTripNotesAndControllersWithinATickOfTheirTimes()
    {
        Random random = new(11);
        for (int round = 0; round < 300; round++)
        {
            double length = 1 + (random.NextDouble() * 20);
            int count = random.Next(0, 60);
            int[] pitches = [.. Enumerable.Range(0, 128).OrderBy(_ => random.Next()).Take(count)];
            MidiNote[] notes = [.. pitches.Select((pitch, index) =>
                Note(index + 1, pitch, random.NextDouble() * (length - 0.05), 0.02 + (random.NextDouble() * 3), random.Next(1, 128)))];
            MidiControlChange[] controls = [.. Enumerable.Range(0, random.Next(0, 10))
                .Select(i => new MidiControlChange(random.NextDouble() * length, 20 + i, random.Next(0, 128))).OrderBy(change => change.Time)];
            double bpm = 60 + (random.NextDouble() * 140);

            MidiClip clip = Clip(random.NextDouble() * 5, length, notes, controls);
            MidiImport import = MidiFile.Read(Write(clip, bpm, Change("tmp-a", 2 + random.NextDouble(), 60 + (random.NextDouble() * 140))));

            Assert.Equal(notes.Length, import.Notes.Count);
            foreach (MidiNote note in notes)
            {
                MidiImportedNote back = import.Notes.Single(candidate => candidate.Pitch == note.Pitch);
                Assert.InRange(Math.Abs(back.Start - note.Start), 0, 0.0022);
                Assert.InRange(Math.Abs(back.Start + back.Duration - Math.Min(note.Start + note.Duration, length)), 0, 0.0045);
                Assert.Equal(note.Velocity, back.Velocity);
            }
            Assert.Equal(controls.Length, import.ControlChanges.Count);
        }
    }

    private static MidiControlChange Rounded(MidiControlChange change) => change with { Time = Math.Round(change.Time, 3) };
}
