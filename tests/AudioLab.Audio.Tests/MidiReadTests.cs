using AudioLab.Audio.Midi;
using AudioLab.Audio.Tests.Support;
using AudioLab.Core.Model;

namespace AudioLab.Audio.Tests;

public sealed class MidiReadTests
{
    private const int Quarter = 480;
    private const int Half = 500_000;

    private static MidiImport Read(byte[] file) => MidiFile.Read(file);

    private static byte[] Format0(Action<MidiBytes> events, int division = Quarter) => MidiBytes.File(0, division, MidiBytes.Track(events));

    [Fact]
    public void TicksBecomeSecondsAtTheDefaultTempoWhenTheFileSetsNone()
    {
        MidiImport import = Read(Format0(t => t.NoteOn(Quarter, 60, 100).NoteOff(Quarter, 60).End()));

        Assert.Equal(120, import.Bpm);
        Assert.Empty(import.TempoChanges);
        MidiImportedNote note = Assert.Single(import.Notes);
        Assert.Equal(new MidiImportedNote(60, 0.5, 0.5, 100), note);
        Assert.Equal(1.0, import.Length, 9);
    }

    [Fact]
    public void ATempoChangeMovesTheSecondsOfWhatComesAfterIt()
    {
        MidiBytes tempo = new();
        tempo.Tempo(0, Half).Tempo(Quarter, 1_000_000).End();
        byte[] file = MidiBytes.File(
            1,
            Quarter,
            tempo.Chunk(),
            MidiBytes.Track(t => t.NoteOn(2 * Quarter, 64, 90).NoteOff(Quarter, 64).End()));

        MidiImport import = Read(file);

        Assert.Equal(120, import.Bpm);
        MidiTempoEvent change = Assert.Single(import.TempoChanges);
        Assert.Equal(0.5, change.Time, 9);
        Assert.Equal(60, change.Bpm, 9);
        Assert.Equal(1.5, Assert.Single(import.Notes).Start, 9);
        Assert.Equal(1.0, import.Notes[0].Duration, 9);
    }

    // Audit row 30: a file whose first tempo event came after the start lost its opening tempo.
    [Fact]
    public void AFirstTempoAfterTheStartLeavesTheStartTempoAt120AndMakesTheEventAChange()
    {
        MidiImport import = Read(Format0(t => t.NoteOn(0, 60, 100).Tempo(Quarter, 1_000_000).NoteOff(Quarter, 60).End()));

        Assert.Equal(120, import.Bpm);
        MidiTempoEvent change = Assert.Single(import.TempoChanges);
        Assert.Equal(0.5, change.Time, 9);
        Assert.Equal(60, change.Bpm, 9);
        Assert.Equal(1.5, import.Length, 9);
    }

    [Fact]
    public void ATempoAtTheStartIsTheStartTempoAndOneUnderTheProjectsFloorFoldsIntoIt()
    {
        MidiImport import = Read(Format0(t => t.Tempo(0, 600_000).Tempo(2, 750_000).NoteOn(Quarter, 60, 100).NoteOff(Quarter, 60).End()));

        Assert.Equal(80, import.Bpm, 9);
        Assert.Empty(import.TempoChanges);
    }

    [Fact]
    public void TempoChangesLessThanAMillisecondApartKeepTheLaterTempoAtTheEarlierTime()
    {
        MidiImport import = Read(Format0(t => t.Tempo(Quarter, 400_000).Tempo(1, 300_000).NoteOn(Quarter, 60, 100).NoteOff(Quarter, 60).End()));

        MidiTempoEvent change = Assert.Single(import.TempoChanges);
        Assert.Equal(200, change.Bpm, 9);
        Assert.Equal(0.5, change.Time, 9);
    }

    [Fact]
    public void ATempoEqualToTheOneInForceIsNotAChange()
    {
        MidiImport import = Read(Format0(t => t.Tempo(Quarter, Half).NoteOn(Quarter, 60, 100).NoteOff(Quarter, 60).End()));

        Assert.Empty(import.TempoChanges);
    }

    [Fact]
    public void RunningStatusRepeatsTheLastStatusAndIsCancelledByAMetaEvent()
    {
        byte[] running = Format0(t => t.Raw(0, 0x90, 60, 100).Raw(0x83, 0x60, 62, 100).Raw(0x00, 60, 0).Raw(0x00, 62, 0).End());

        MidiImport import = Read(running);

        Assert.Equal([60, 62], import.Notes.Select(note => note.Pitch));
        Assert.Equal(0, import.IgnoredNoteOffs);

        byte[] afterMeta = Format0(t => t.Raw(0, 0x90, 60, 100).Raw(0, 0xFF, 0x01, 0x00).Raw(0, 60, 0).End());
        MidiFormatException refused = Assert.Throws<MidiFormatException>(() => Read(afterMeta));
        Assert.Contains("running status", refused.Message);
    }

    [Fact]
    public void ANoteOnWithVelocityZeroEndsTheNoteAndAStrayNoteOffIsCountedAndLeftOut()
    {
        MidiImport import = Read(Format0(t => t.NoteOff(0, 70).NoteOn(0, 60, 100).NoteOn(Quarter, 60, 0).End()));

        Assert.Equal(new MidiImportedNote(60, 0, 0.5, 100), Assert.Single(import.Notes));
        Assert.Equal(1, import.IgnoredNoteOffs);
        Assert.Equal(0, import.UnclosedNotes);
    }

    [Fact]
    public void ANoteNeverEndedLastsToTheEndOfItsTrackAndIsCounted()
    {
        MidiImport import = Read(Format0(t => t.NoteOn(0, 60, 100).End(Quarter * 2)));

        Assert.Equal(new MidiImportedNote(60, 0, 1.0, 100), Assert.Single(import.Notes));
        Assert.Equal(1, import.UnclosedNotes);
    }

    [Fact]
    public void ANoteThatStartsAndEndsOnOneTickIsLengthenedToTheLeastAPianoRollHolds()
    {
        MidiImport import = Read(Format0(t => t.NoteOn(0, 60, 100).NoteOff(0, 60).End()));

        Assert.Equal(0.02, Assert.Single(import.Notes).Duration, 9);
        Assert.True(import.Length >= 0.05);
    }

    [Fact]
    public void OverlappingNotesOfOnePitchEndInTheOrderTheyStartedAndChannelsAreSeparate()
    {
        MidiImport import = Read(Format0(t => t
            .NoteOn(0, 60, 50).NoteOn(Quarter, 60, 80).NoteOff(Quarter, 60).NoteOff(Quarter, 60)
            .NoteOn(0, 60, 90, channel: 1).NoteOff(Quarter, 60, channel: 1).End()));

        Assert.Equal(
            [new MidiImportedNote(60, 0, 1.0, 50), new MidiImportedNote(60, 0.5, 1.0, 80), new MidiImportedNote(60, 1.5, 0.5, 90)],
            import.Notes);
    }

    [Fact]
    public void ControllersOfEveryTrackAreKeptInTimeOrderAndTheLaterOfTwoAtOneTickAndNumberWins()
    {
        MidiImport import = Read(Format0(t => t
            .Controller(Quarter, 64, 127).Controller(0, 64, 0).Controller(0, 1, 40).Controller(Quarter, 64, 100).End()));

        Assert.Equal(
            [new MidiControlChange(0.5, 64, 0), new MidiControlChange(0.5, 1, 40), new MidiControlChange(1.0, 64, 100)],
            import.ControlChanges);
    }

    [Fact]
    public void TheFirstTimeSignatureIsKeptAndOneAProjectCannotHoldIsNot()
    {
        Assert.Equal(new TimeSignature(6, 8), Read(Format0(t => t.TimeSignature(0, 6, 3).TimeSignature(Quarter, 3, 2).End())).TimeSignature);
        Assert.Null(Read(Format0(t => t.TimeSignature(0, 4, 6).End())).TimeSignature);
        Assert.Null(Read(Format0(t => t.TimeSignature(0, 65, 2).End())).TimeSignature);
        Assert.Null(Read(Format0(t => t.End())).TimeSignature);
    }

    [Fact]
    public void ADifferentDivisionChangesTheTimeOfATick()
    {
        MidiImport import = Read(Format0(t => t.NoteOn(96, 60, 100).NoteOff(96, 60).End(), division: 96));

        Assert.Equal(new MidiImportedNote(60, 0.5, 0.5, 100), Assert.Single(import.Notes));
    }

    [Fact]
    public void SysexMetaTextAndOtherChannelEventsAreSkippedByTheirLengthsAndTheNotesAroundThemRead()
    {
        byte[] file = Format0(t => t
            .Raw(0, 0xFF, 0x03, 0x04, (byte)'n', (byte)'a', (byte)'m', (byte)'e')
            .Raw(0, 0xF0, 0x03, 1, 2, 0xF7).Raw(0, 0xC0, 5).Raw(0, 0xE0, 0, 0x40).Raw(0, 0xD0, 9).Raw(0, 0xA0, 60, 10)
            .NoteOn(0, 60, 100).NoteOff(Quarter, 60).End());

        Assert.Equal(new MidiImportedNote(60, 0, 0.5, 100), Assert.Single(Read(file).Notes));
    }

    [Fact]
    public void AClipMadeFromTheImportPassesTheProjectValidator()
    {
        MidiImport import = Read(Format0(t => t
            .NoteOn(0, 60, 100).NoteOff(Quarter, 60).Controller(0, 64, 127).NoteOn(0, 64, 90).NoteOn(Quarter, 67, 80).End(Quarter)));
        MidiClip clip = import.ToClip(ClipId.From("clp-imported"), "Imported", 4);
        Track track = TestProject.Track("t", TrackKind.Instrument) with { MidiClips = [clip], Instrument = InstrumentPresets.CreateSynth("Pluck") };

        Assert.Empty(ProjectValidator.Validate(TestProject.Create(track)));
        Assert.Equal([1, 2, 3], clip.Notes.Select(note => note.Id.Value));
        Assert.Equal(4, clip.NextNoteId.Value);
    }

    // Audit row 47: a take or import with about 125,000 notes threw.
    [Fact]
    public void AHundredAndThirtyThousandNotesReadAndMakeAValidClip()
    {
        MidiBytes events = new();
        for (int i = 0; i < 130_000; i++)
        {
            events.NoteOn(i == 0 ? 0 : 1, 30 + (i % 60), 1 + (i % 127)).NoteOff(i % 7 + 1, 30 + (i % 60));
        }
        MidiImport import = Read(MidiBytes.File(0, Quarter, events.End().Chunk()));

        Assert.Equal(130_000, import.Notes.Count);
        MidiClip clip = import.ToClip(ClipId.From("clp-big"), "Big", 0);
        Track track = TestProject.Track("t", TrackKind.Instrument) with { MidiClips = [clip], Instrument = InstrumentPresets.CreateSynth("Organ") };
        Assert.Empty(ProjectValidator.Validate(TestProject.Create(track)));
    }

    [Fact]
    public void EveryTrackOfAFormat1FileGoesIntoOneListOrderedByStart()
    {
        byte[] file = MidiBytes.File(
            1,
            Quarter,
            MidiBytes.Track(t => t.NoteOn(Quarter, 72, 100).NoteOff(Quarter, 72).End()),
            MidiBytes.Track(t => t.NoteOn(0, 40, 100, channel: 9).NoteOff(Quarter, 40, channel: 9).End()));

        MidiImport import = Read(file);

        Assert.Equal([40, 72], import.Notes.Select(note => note.Pitch));
    }

    [Fact]
    public void AChunkOfAnotherTypeBetweenTracksIsSkipped()
    {
        byte[] other = new MidiBytes().Raw(1, 2, 3).Chunk("XFIH");
        byte[] file = MidiBytes.FileDeclaring(
            1,
            2,
            Quarter,
            MidiBytes.Track(t => t.NoteOn(0, 60, 100).NoteOff(Quarter, 60).End()),
            other,
            MidiBytes.Track(t => t.End()));

        Assert.Single(Read(file).Notes);
    }

    [Fact]
    public void AHeaderLongerThanSixBytesHasItsExtraBytesSkipped()
    {
        byte[] file = MidiBytes.File(0, Quarter, MidiBytes.Track(t => t.NoteOn(0, 60, 100).NoteOff(Quarter, 60).End()));
        byte[] longer = [.. file[..14], 0xAA, 0xBB, .. file[14..]];
        longer[7] = 8;

        Assert.Single(Read(longer).Notes);
    }

    [Fact]
    public void ChannelModeControllersAreLeftOutAndOrdinaryOnesAreKept()
    {
        MidiImport import = Read(Format0(t => t.Controller(0, 123, 0).Controller(0, 121, 0).Controller(0, 120, 0).Controller(0, 119, 5).End()));

        Assert.Equal(new MidiControlChange(0, 119, 5), Assert.Single(import.ControlChanges));
    }

    [Fact]
    public void ATempoThatFoldsBackToTheOneBeforeItLeavesNoChange()
    {
        MidiImport import = Read(Format0(t => t.Tempo(Quarter, 400_000).Tempo(1, Half).NoteOn(Quarter, 60, 100).NoteOff(Quarter, 60).End()));

        Assert.Empty(import.TempoChanges);
        Assert.Equal(120, import.Bpm);
    }
}
