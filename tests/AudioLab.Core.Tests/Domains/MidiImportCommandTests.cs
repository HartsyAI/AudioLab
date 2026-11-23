using AudioLab.Core.Commands;
using AudioLab.Core.Model;

namespace AudioLab.Core.Tests;

public sealed class MidiImportCommandTests
{
    private readonly TrackId _keys = TrackId.From("trk_keys");

    private readonly CommandRig _rig;

    public MidiImportCommandTests()
    {
        Track keys = ProjectDefaults.NewTrack(_keys, "Keys", TrackKind.Instrument, "#abc", InstrumentPresets.CreateSynth("Soft Keys"));
        _rig = new CommandRig(BlankProject.Create() with { Tracks = [keys, BlankProject.Track("trk_audio", "Audio")] });
    }

    private ImportMidiClip Import(
        IReadOnlyList<ImportedNote>? notes = null,
        IReadOnlyList<ImportedControlChange>? controls = null,
        double length = 4,
        string? name = null,
        TrackId? track = null)
    {
        return new ImportMidiClip
        {
            Project = _rig.Id,
            Track = track ?? _keys,
            Start = 2,
            Length = length,
            Notes = notes ?? [new ImportedNote(60, 0, 0.5, 100)],
            ControlChanges = controls ?? [],
            Name = name,
        };
    }

    private MidiClip[] Clips => [.. _rig.Project.Tracks.First(track => track.Id == _keys).MidiClips];

    [Fact]
    public void AClipComesWithItsNotesAndEventsAndTheNextNoteIdAfterThem()
    {
        ImportedNote[] notes = [new(60, 0, 0.5, 100), new(64, 0.5, 0.25, 90), new(67, 1, 1, 1)];
        ImportedControlChange[] controls = [new(0, 64, 127), new(2, 64, 0), new(2, 1, 5)];

        IReadOnlyList<string> created = _rig.Created(Import(notes, controls, name: "  Take 1 "));

        MidiClip clip = Assert.Single(Clips);
        Assert.Equal([clip.Id.Value], created);
        Assert.Equal(("Take 1", 2.0, 4.0), (clip.Name, clip.Start, clip.Length));
        Assert.Equal(
            [new MidiNote(new NoteId(1), 60, 0, 0.5, 100), new MidiNote(new NoteId(2), 64, 0.5, 0.25, 90), new MidiNote(new NoteId(3), 67, 1, 1, 1)],
            clip.Notes);
        Assert.Equal([new MidiControlChange(0, 64, 127), new MidiControlChange(2, 64, 0), new MidiControlChange(2, 1, 5)], clip.ControlChanges);
        Assert.Equal(new NoteId(4), clip.NextNoteId);
    }

    [Fact]
    public void TheWholeImportIsOneUndoStepAndRedoKeepsTheIds()
    {
        _rig.Ok(Import());
        string id = Clips[0].Id.Value;

        Assert.Equal(1, _rig.History.UndoCount);
        _rig.History.Undo();
        Assert.Empty(Clips);
        _rig.History.Redo();
        Assert.Equal(id, Clips[0].Id.Value);
    }

    [Fact]
    public void AnEmptyFileMakesAnEmptyClipAndABlankNameGetsTheNextOne()
    {
        _rig.Ok(Import(notes: [], name: " "));
        _rig.Ok(Import(notes: [], name: null));

        Assert.Equal(["MIDI 1", "MIDI 2"], Clips.Select(clip => clip.Name));
        Assert.All(Clips, clip => Assert.Equal(NoteId.First, clip.NextNoteId));
    }

    [Fact]
    public void ATrackThatIsNotThereIsNotFoundAndAnAudioTrackIsRejected()
    {
        _rig.RefusedWith(Import(track: TrackId.From("ghost")), CommandErrorCode.NotFound);
        _rig.RefusedWith(Import(track: TrackId.From("trk_audio")), CommandErrorCode.Rejected);
    }

    [Theory]
    [InlineData(128, 0, 0.5, 100)]
    [InlineData(-1, 0, 0.5, 100)]
    [InlineData(60, -0.1, 0.5, 100)]
    [InlineData(60, 3.99, 0.5, 100)]
    [InlineData(60, double.NaN, 0.5, 100)]
    [InlineData(60, 0, 0.019, 100)]
    [InlineData(60, 0, double.PositiveInfinity, 100)]
    [InlineData(60, 0, 0.5, 0)]
    [InlineData(60, 0, 0.5, 128)]
    public void ANoteThatTheNoteCommandsWouldRefuseIsRefusedAndNothingIsAdded(int pitch, double start, double duration, int velocity)
    {
        _rig.RefusedWith(Import([new ImportedNote(60, 0, 1, 100), new ImportedNote(pitch, start, duration, velocity)]), CommandErrorCode.Invalid);
    }

    [Fact]
    public void ARefusalNamesTheNoteThatCausedIt()
    {
        CommandResult result = _rig.Refused(Import([new ImportedNote(60, 0, 1, 100), new ImportedNote(200, 0, 1, 100)]));

        Assert.Contains("Note 2", result.Errors[0].Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(-1, 64, 0)]
    [InlineData(4.1, 64, 0)]
    [InlineData(1, 128, 0)]
    [InlineData(1, 64, 128)]
    [InlineData(double.NaN, 64, 0)]
    public void AControllerEventOutsideTheClipOrTheRangeIsRefused(double time, int controller, int value)
    {
        _rig.RefusedWith(Import(controls: [new ImportedControlChange(time, controller, value)]), CommandErrorCode.Invalid);
    }

    [Fact]
    public void ControllerEventsOutOfOrderOrRepeatedAreRefused()
    {
        _rig.RefusedWith(Import(controls: [new(2, 64, 1), new(1, 64, 0)]), CommandErrorCode.Invalid);
        _rig.RefusedWith(Import(controls: [new(1, 64, 1), new(1, 64, 0)]), CommandErrorCode.Invalid);
    }

    [Theory]
    [InlineData(0.04)]
    [InlineData(double.NaN)]
    [InlineData(2_000_000)]
    public void AClipOfAnImpossibleLengthIsRefused(double length)
    {
        _rig.RefusedWith(Import(notes: [], length: length), CommandErrorCode.Invalid);
    }

    [Fact]
    public void ANameLongerThanTheModelAllowsIsRefused()
    {
        _rig.RefusedWith(Import(name: new string('x', ProjectLimits.MaxName + 1)), CommandErrorCode.Invalid);
    }

    [Fact]
    public void MoreNotesOrEventsThanAClipMayHoldAreRefusedBeforeAnyIsLookedAt()
    {
        ImportedNote[] tooManyNotes = new ImportedNote[ProjectLimits.MaxListItems + 1];
        ImportedControlChange[] tooManyEvents = new ImportedControlChange[ProjectLimits.MaxListItems + 1];

        _rig.RefusedWith(Import(tooManyNotes), CommandErrorCode.Invalid);
        _rig.RefusedWith(Import(controls: tooManyEvents), CommandErrorCode.Invalid);
    }

    [Fact]
    public void TheCommandIsOnTheWireWithADescribedSchemaAndRoundTripsThroughJson()
    {
        CommandRegistry registry = CoreCommands.CreateRegistry();
        string json = $"{{\"project\":\"{_rig.Id.Value}\",\"track\":\"trk_keys\",\"start\":1,\"length\":2,"
            + "\"notes\":[{\"pitch\":60,\"start\":0,\"duration\":0.5,\"velocity\":100}],"
            + "\"controlChanges\":[{\"time\":0,\"controller\":64,\"value\":127}]}";

        CommandParseResult parsed = registry.Parse("midi_clip_import", json);

        ImportMidiClip command = Assert.IsType<ImportMidiClip>(parsed.Command);
        Assert.Equal([new ImportedNote(60, 0, 0.5, 100)], command.Notes);
        Assert.Equal([new ImportedControlChange(0, 64, 127)], command.ControlChanges);
        string withId = json.Replace("\"pitch\":60", "\"pitch\":60,\"id\":1", StringComparison.Ordinal);
        Assert.NotEmpty(registry.Parse("midi_clip_import", withId).Errors);
        string withoutVelocity = json.Replace("\"velocity\":100", string.Empty, StringComparison.Ordinal);
        Assert.NotEmpty(registry.Parse("midi_clip_import", withoutVelocity).Errors);
    }

    [Fact]
    public void ARefusedImportNeverSpendsAnId()
    {
        CommandRig plain = new(_rig.Project);
        CommandRig refused = new(_rig.Project);
        refused.RefusedWith(Import([new ImportedNote(200, 0, 1, 100)]), CommandErrorCode.Invalid);
        refused.RefusedWith(Import(track: TrackId.From("trk_audio")), CommandErrorCode.Rejected);

        string next = refused.Created(Import())[0];

        Assert.Equal(plain.Created(Import())[0], next);
    }

    [Fact]
    public void AMillionNotesAreOneValidatedUndoStepWithinAGenerousBound()
    {
        ImportedNote[] notes =
        [
            .. Enumerable.Range(0, ProjectLimits.MaxListItems).Select(index => new ImportedNote(index % 128, index * 0.003, 0.02, 1 + (index % 127))),
        ];
        ImportedControlChange[] events = [.. Enumerable.Range(0, 1000).Select(index => new ImportedControlChange(index * 3.0, 64, index % 128))];
        System.Diagnostics.Stopwatch watch = System.Diagnostics.Stopwatch.StartNew();

        string clip = _rig.Created(Import(notes, events, length: 3001))[0];

        // A bound that a slow machine meets by a wide margin, so that only a quadratic path would miss it.
        Assert.True(watch.Elapsed < TimeSpan.FromMinutes(2), $"one import of a million notes took {watch.Elapsed}");
        MidiClip imported = Clips.Single();
        NoteId expectedNext = new(ProjectLimits.MaxListItems + 1);
        Assert.Equal((clip, ProjectLimits.MaxListItems, expectedNext), (imported.Id.Value, imported.Notes.Count, imported.NextNoteId));
        Assert.Equal(1, _rig.History.UndoCount);
        _rig.History.Undo();
        Assert.Empty(Clips);
    }
}
