using AudioLab.Audio.Midi;
using AudioLab.Audio.Tests.Support;

namespace AudioLab.Audio.Tests;

public sealed class MidiRefusalTests
{
    private static byte[] Whole() => MidiBytes.File(0, 480, MidiBytes.Track(t => t.NoteOn(0, 60, 100).NoteOff(480, 60).End()));

    private static byte[] OneTrack(Action<MidiBytes> events) => MidiBytes.File(0, 480, MidiBytes.Track(events));

    private static void Refused(byte[] file, string reason)
    {
        MidiFormatException refusal = Assert.Throws<MidiFormatException>(() => MidiFile.Read(file));
        Assert.Contains(reason, refusal.Message);
    }

    [Fact]
    public void AWholeFileIsAccepted() => Assert.Single(MidiFile.Read(Whole()).Notes);

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    [InlineData(13)]
    public void AFileCutInsideItsHeaderIsRefused(int length) => Refused(Whole()[..length], "MThd");

    [Fact]
    public void AFileThatIsNotMidiIsRefused() => Refused("RIFF....WAVEfmt "u8.ToArray(), "MThd");

    [Fact]
    public void AFileCutInsideItsTrackIsRefusedAtEveryLength()
    {
        byte[] whole = Whole();
        for (int length = 14; length < whole.Length; length++)
        {
            Assert.Throws<MidiFormatException>(() => MidiFile.Read(whole[..length]));
        }
    }

    [Fact]
    public void ABytePastTheLastChunkIsRefused() => Refused([.. Whole(), 0], "bytes after its last chunk");

    [Fact]
    public void AChunkThatDeclaresMoreThanTheFileHoldsIsRefused()
    {
        byte[] file = Whole();
        file[18] = 0x7F;

        Refused(file, "past the end of the file");
    }

    [Fact]
    public void AHeaderThatDeclaresMoreThanTheFileHoldsIsRefused()
    {
        byte[] file = Whole();
        file[7] = 0xFF;

        Refused(file, "declares 255 bytes");
    }

    [Fact]
    public void Format2AndSmpteTimingAndZeroDivisionAreRefused()
    {
        byte[] track = MidiBytes.Track(t => t.End());

        Refused(MidiBytes.File(2, 480, track), "format 2");
        Refused(MidiBytes.File(0, 0xE728, track), "SMPTE");
        Refused(MidiBytes.File(0, 0, track), "0 ticks");
    }

    [Fact]
    public void ATrackCountThatDisagreesWithTheChunksIsRefused()
    {
        byte[] track = MidiBytes.Track(t => t.End());

        Refused(MidiBytes.FileDeclaring(1, 2, 480, track), "declares 2 tracks and the file holds 1");
        Refused(MidiBytes.FileDeclaring(1, 1, 480, track, track), "declares 1 tracks and the file holds 2");
        Refused(MidiBytes.File(0, 480, track, track), "format 0 file has one track");
        Refused(MidiBytes.FileDeclaring(1, 0, 480), "0 tracks");
        Refused(MidiBytes.FileDeclaring(1, MidiLimits.MaxTracks + 1, 480, track), "tracks");
    }

    [Fact]
    public void ATrackWithNoEndOfTrackIsRefused() => Refused(OneTrack(t => t.NoteOn(0, 60, 100)), "End of Track");

    [Fact]
    public void AnEmptyTrackIsRefused() => Refused(OneTrack(_ => { }), "End of Track");

    [Fact]
    public void AnEventAfterEndOfTrackIsRefused() => Refused(OneTrack(t => t.End().NoteOn(0, 60, 100)), "after End of Track");

    [Fact]
    public void AnEndOfTrackWithABodyIsRefused() => Refused(OneTrack(t => t.Raw(0, 0xFF, 0x2F, 0x01, 0x00)), "End of Track event has 1 bytes");

    [Fact]
    public void AVariableLengthNumberOfFiveBytesIsRefused() =>
        Refused(OneTrack(t => t.Raw(0x81, 0x80, 0x80, 0x80, 0x00).End()), "more than four bytes");

    [Fact]
    public void ADeltaOfFourFullBytesIsAcceptedAndATimeOverAMillionSecondsIsRefused()
    {
        MidiImport import = MidiFile.Read(
            OneTrack(t => t.NoteOn(0, 60, 100).NoteOff(1000, 60).Delta(MidiLimits.MaxVariableLength).Raw(0xFF, 0x2F, 0x00)));

        Assert.True(import.Length > 279_000);
        Refused(
            OneTrack(t => t.Tempo(0, 1_000_000).Delta(MidiLimits.MaxVariableLength).Raw(0xFF, 0x01, 0x00)
                .Delta(MidiLimits.MaxVariableLength).Raw(0xFF, 0x01, 0x00).End()),
            "lasts longer than");
    }

    [Fact]
    public void ADataByteWithNoRunningStatusIsRefused() => Refused(OneTrack(t => t.Raw(0, 60, 100).End()), "no running status");

    [Fact]
    public void ADataByteWithItsHighBitSetIsRefused()
    {
        Refused(OneTrack(t => t.Raw(0, 0x90, 60, 0x80).End()), "high bit");
        Refused(OneTrack(t => t.Raw(0, 0x90, 0xC8, 100).End()), "high bit");
    }

    [Fact]
    public void AnEventThatCrossesTheEndOfItsTrackIsRefused()
    {
        Refused(OneTrack(t => t.Raw(0, 0x90, 60)), "past the end of the track");
        Refused(OneTrack(t => t.Raw(0, 0xFF, 0x03, 0x10, 1, 2)), "past the end of the track");
        Refused(OneTrack(t => t.Raw(0, 0xF0, 0x10, 1, 2)), "past the end of the track");
        Refused(OneTrack(t => t.Raw(0, 0xFF, 0x03, 0xFF, 0xFF, 0xFF, 0x7F)), "past the end of the track");
    }

    [Fact]
    public void ASystemMessageThatIsNotSysexIsRefused() => Refused(OneTrack(t => t.Raw(0, 0xF8).End()), "system message F8");

    [Fact]
    public void MetaEventsOfTheWrongLengthAreRefused()
    {
        Refused(OneTrack(t => t.Raw(0, 0xFF, 0x51, 0x02, 1, 2).End()), "tempo event has 2 bytes, not 3");
        Refused(OneTrack(t => t.Raw(0, 0xFF, 0x58, 0x03, 4, 2, 24).End()), "time signature event has 3 bytes, not 4");
        Refused(OneTrack(t => t.Raw(0, 0xFF, 0x59, 0x01, 0).End()), "meta 59 event has 1 bytes, not 2");
        Refused(OneTrack(t => t.Raw(0, 0xFF, 0x54, 0x04, 0, 0, 0, 0).End()), "meta 54");
        Refused(OneTrack(t => t.Raw(0, 0xFF, 0x20, 0x00).End()), "meta 20");
        Refused(OneTrack(t => t.Raw(0, 0xFF, 0x00, 0x01, 0).End()), "sequence number");
    }

    [Fact]
    public void ATempoOfZeroOrAbove1000BeatsAMinuteIsRefusedAndTheEdgesAreAccepted()
    {
        Refused(OneTrack(t => t.Tempo(0, 0).End()), "above 1000 beats a minute");
        Refused(OneTrack(t => t.Tempo(0, MidiLimits.MinMicrosecondsPerQuarter - 1).End()), "above 1000 beats a minute");
        Assert.Equal(1000, MidiFile.Read(OneTrack(t => t.Tempo(0, MidiLimits.MinMicrosecondsPerQuarter).End())).Bpm, 9);
        Assert.Equal(3.576, MidiFile.Read(OneTrack(t => t.Tempo(0, MidiLimits.MaxMicrosecondsPerQuarter).End())).Bpm, 3);
    }

    [Fact]
    public void TheMessageNamesTheTrackThatIsBad()
    {
        byte[] good = MidiBytes.Track(t => t.End());
        byte[] bad = MidiBytes.Track(t => t.Raw(0, 0x90, 60));

        MidiFormatException refusal = Assert.Throws<MidiFormatException>(() => MidiFile.Read(MidiBytes.File(1, 480, good, bad)));

        Assert.Contains("MIDI track 2", refusal.Message);
    }

    [Fact]
    public void AFileOverTheLimitIsRefusedBeforeItIsRead()
    {
        byte[] file = new byte[MidiLimits.MaxFileBytes + 1];

        Refused(file, "over the limit");
    }

    [Fact]
    public void OneNoteOverTheLimitIsRefusedAndTheLimitItselfIsAccepted()
    {
        MidiBytes atLimit = new();
        atLimit.Raw(0, 0x90, 60, 100);
        for (int i = 1; i < MidiLimits.MaxNotes; i++)
        {
            atLimit.Raw(0, (byte)(i % 100), 100);
        }
        MidiBytes over = new();
        over.Raw(0, 0x90, 60, 100);
        for (int i = 1; i <= MidiLimits.MaxNotes; i++)
        {
            over.Raw(0, (byte)(i % 100), 100);
        }

        Assert.Equal(MidiLimits.MaxNotes, MidiFile.Read(MidiBytes.File(0, 480, atLimit.End(1).Chunk())).Notes.Count);
        Refused(MidiBytes.File(0, 480, over.End(1).Chunk()), "more than 1000000 notes");
    }

    [Fact]
    public void OneControllerEventOverTheLimitIsRefused()
    {
        MidiBytes events = new();
        events.Raw(0, 0xB0, 1, 0);
        for (int i = 1; i <= MidiLimits.MaxControlChanges; i++)
        {
            events.Raw(0, 1, (byte)(i % 100));
        }

        Refused(MidiBytes.File(0, 480, events.End().Chunk()), "more than 1000000 controller events");
    }

    [Fact]
    public void OneTempoEventOverTheLimitIsRefused()
    {
        MidiBytes events = new();
        for (int i = 0; i <= MidiLimits.MaxTempoEvents; i++)
        {
            events.Tempo(1, 400_000 + i);
        }

        Refused(MidiBytes.File(0, 480, events.End().Chunk()), "more than 100000 tempo events");
    }

    // Whatever the bytes, the reader returns or refuses with its own exception: no other exception, no hang, no huge allocation.
    [Fact]
    public void ChangedAndCutBytesOfAValidFileNeverEscapeAsAnotherException()
    {
        MidiBytes events = new();
        events.Tempo(0, 500_000).TimeSignature(0, 4, 2).NoteOn(0, 60, 100).Controller(10, 64, 127).NoteOff(480, 60).Controller(0, 64, 0).End();
        byte[] valid = MidiBytes.File(1, 480, events.Chunk(), MidiBytes.Track(t => t.NoteOn(0, 40, 90).NoteOff(96, 40).End()));
        Random random = new(7);
        for (int round = 0; round < 20_000; round++)
        {
            byte[] changed = (byte[])valid.Clone();
            int edits = 1 + random.Next(3);
            for (int edit = 0; edit < edits; edit++)
            {
                changed[random.Next(changed.Length)] = (byte)random.Next(256);
            }
            ReadOrRefuse(random.Next(4) == 0 ? changed[..random.Next(changed.Length)] : changed);
        }
    }

    private static void ReadOrRefuse(byte[] file)
    {
        try
        {
            MidiImport import = MidiFile.Read(file);
            Assert.True(import.Length >= 0.05 && import.Notes.All(note => note.Duration >= 0.02 && note.Pitch is >= 0 and <= 127));
        }
        catch (MidiFormatException)
        {
            // A refusal is the other allowed outcome.
        }
    }

    // Sizes come from what has been read, never from what a file declares, so a small file that declares the largest of everything stays small.
    [Fact]
    public void AFileThatDeclaresTheLargestOfEverythingCostsLittleToRefuse()
    {
        byte[] header = [.. "MThd"u8.ToArray(), 0, 0, 0, 6, 0, 1, 0xFF, 0xFF, 0x01, 0xE0];
        byte[] hugeChunk = [.. MidiBytes.File(0, 480), .. "MTrk"u8.ToArray(), 0xFF, 0xFF, 0xFF, 0xFF, 1, 2, 3];
        byte[] hugeMeta = OneTrack(t => t.Raw(0, 0xFF, 0x03, 0xFF, 0xFF, 0xFF, 0x7F));
        byte[] hugeSysex = OneTrack(t => t.Raw(0, 0xF0, 0xFF, 0xFF, 0xFF, 0x7F));
        byte[] hugeDelta = OneTrack(t => t.Raw(0xFF, 0xFF, 0xFF, 0x7F, 0xFF, 0x2F, 0x00));
        byte[] hugeHeader = [.. "MThd"u8.ToArray(), 0xFF, 0xFF, 0xFF, 0xFF, 0, 0, 0, 1, 1, 0xE0];
        byte[][] files = [header, hugeChunk, hugeMeta, hugeSysex, hugeDelta, hugeHeader];
        foreach (byte[] file in files)
        {
            Assert.True(file.Length < 1024);
            TryRead(file);
        }

        long before = GC.GetAllocatedBytesForCurrentThread();
        foreach (byte[] file in files)
        {
            TryRead(file);
        }
        long used = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.True(used < 64 * 1024, $"Refusing six small files allocated {used} bytes.");
    }

    private static void TryRead(byte[] file)
    {
        try
        {
            MidiFile.Read(file);
        }
        catch (MidiFormatException)
        {
            // Every one of these files is refused; the cost of that is what is measured.
        }
    }
}
