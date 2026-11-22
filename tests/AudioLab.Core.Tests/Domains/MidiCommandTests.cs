using AudioLab.Core.Commands;
using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Core.Tests;

public sealed class MidiCommandTests
{
    private readonly TrackId _keys = TrackId.From("trk_keys");

    private readonly TrackId _audio = TrackId.From("trk_audio");

    private readonly CommandRig _rig;

    private readonly ClipId _clip;

    public MidiCommandTests()
    {
        Track keys = ProjectDefaults.NewTrack(_keys, "Keys", TrackKind.Instrument, "#abc", InstrumentPresets.CreateSynth("Soft Keys"));
        ProjectDocument start = BlankProject.Create() with { Tracks = [keys, BlankProject.Track("trk_audio", "Audio")] };
        _rig = new CommandRig(start);
        _clip = ClipId.From(_rig.Created(new AddMidiClip { Project = _rig.Id, Track = _keys, Start = 0, Length = 4 })[0]);
    }

    private MidiClip Clip => _rig.Project.Tracks.First(track => track.Id == _keys).MidiClips[0];

    private NoteId Add(int pitch, double start, double? duration = null, int? velocity = null)
    {
        string created = _rig.Created(new AddNote { Project = _rig.Id, Clip = _clip, Pitch = pitch, Start = start, Duration = duration, Velocity = velocity })[0];
        return new NoteId(int.Parse(created, System.Globalization.CultureInfo.InvariantCulture));
    }

    private MidiNote Note(NoteId id) => Clip.Notes.First(note => note.Id == id);

    // ----- add

    [Fact]
    public void ANoteIsAddedWithTheNextNoteIdAFullVelocityOfOneHundredAndOneGridStep()
    {
        NoteId first = Add(60, 0.5);
        NoteId second = Add(64, 1);

        Assert.Equal((1, 2), (first.Value, second.Value));
        Assert.Equal(new MidiNote(first, 60, 0.5, 0.5, 100), Note(first));
        Assert.Equal(new NoteId(3), Clip.NextNoteId);
    }

    [Fact]
    public void ADeletedNotesIdIsNeverGivenToAnotherAndRedoKeepsIds()
    {
        NoteId first = Add(60, 0);
        Add(62, 1);
        _rig.Ok(new DeleteNotes { Project = _rig.Id, Clip = _clip, Notes = [first] });

        NoteId third = Add(64, 2);

        Assert.Equal(3, third.Value);
        _rig.History.Undo();
        _rig.History.Redo();
        Assert.Equal(third, Clip.Notes[^1].Id);
    }

    [Fact]
    public void TheDefaultLengthIsTheGridStepAtTheTempoWhereTheClipStartsNotAtThePlayhead()
    {
        CommandRig rig = new(_rig.Project, 5000);
        rig.Ok(new AddTempoChange { Project = rig.Id, Time = 1, Bpm = 60 });
        ClipId early = ClipId.From(rig.Created(new AddMidiClip { Project = rig.Id, Track = _keys, Start = 0, Length = 4 })[0]);
        ClipId late = ClipId.From(rig.Created(new AddMidiClip { Project = rig.Id, Track = _keys, Start = 2, Length = 4 })[0]);

        rig.Ok(new AddNote { Project = rig.Id, Clip = early, Pitch = 60, Start = 0 });
        rig.Ok(new AddNote { Project = rig.Id, Clip = late, Pitch = 60, Start = 0 });

        MidiClip[] clips = [.. rig.Project.Tracks.First(track => track.Id == _keys).MidiClips];
        Assert.Equal(0.5, clips.First(clip => clip.Id == early).Notes[0].Duration);
        Assert.Equal(1.0, clips.First(clip => clip.Id == late).Notes[0].Duration);
    }

    [Fact]
    public void ANoteOutOfRangeOrOutsideTheClipIsRefused()
    {
        _rig.RefusedWith(new AddNote { Project = _rig.Id, Clip = _clip, Pitch = 128, Start = 0 }, CommandErrorCode.Invalid);
        _rig.RefusedWith(new AddNote { Project = _rig.Id, Clip = _clip, Pitch = -1, Start = 0 }, CommandErrorCode.Invalid);
        _rig.RefusedWith(new AddNote { Project = _rig.Id, Clip = _clip, Pitch = 60, Start = -0.1 }, CommandErrorCode.Invalid);
        _rig.RefusedWith(new AddNote { Project = _rig.Id, Clip = _clip, Pitch = 60, Start = 4 }, CommandErrorCode.Invalid);
        _rig.RefusedWith(new AddNote { Project = _rig.Id, Clip = _clip, Pitch = 60, Start = double.NaN }, CommandErrorCode.Invalid);
        _rig.RefusedWith(new AddNote { Project = _rig.Id, Clip = _clip, Pitch = 60, Start = 0, Velocity = 0 }, CommandErrorCode.Invalid);
        _rig.RefusedWith(new AddNote { Project = _rig.Id, Clip = _clip, Pitch = 60, Start = 0, Velocity = 128 }, CommandErrorCode.Invalid);
        _rig.RefusedWith(new AddNote { Project = _rig.Id, Clip = _clip, Pitch = 60, Start = 0, Duration = 0.01 }, CommandErrorCode.Invalid);
    }

    [Fact]
    public void ANoteGoesOnlyIntoAMidiClipThatExists()
    {
        ClipId audio = ClipId.From(_rig.Created(new AddClip { Project = _rig.Id, Track = _audio, Media = MediaId.From("med_1"), Duration = 3 })[0]);

        _rig.RefusedWith(new AddNote { Project = _rig.Id, Clip = audio, Pitch = 60, Start = 0 }, CommandErrorCode.Rejected);
        _rig.RefusedWith(new AddNote { Project = _rig.Id, Clip = ClipId.From("clp_none"), Pitch = 60, Start = 0 }, CommandErrorCode.NotFound);
    }

    // ----- move, resize, velocity, transpose

    [Fact]
    public void NotesAreMovedToTheirTargetsAndOnlyTheNamedOnesMove()
    {
        NoteId a = Add(60, 1);
        NoteId b = Add(64, 2);

        _rig.Ok(new MoveNotes { Project = _rig.Id, Clip = _clip, Notes = [new NoteTarget(a, 1.5, 63)] });

        Assert.Equal((1.5, 63), (Note(a).Start, Note(a).Pitch));
        Assert.Equal((2.0, 64), (Note(b).Start, Note(b).Pitch));
        _rig.Ok(new MoveNotes { Project = _rig.Id, Clip = _clip, Notes = [new NoteTarget(a, 1.75, 63), new NoteTarget(b, 2.25, 70)] });
        Assert.Equal((1.75, 2.25, 70), (Note(a).Start, Note(b).Start, Note(b).Pitch));
    }

    [Fact]
    public void ATargetPastTheLatestStartIsKeptInsideTheClipAndOneOutsideTheProjectsRangeIsRefused()
    {
        NoteId id = Add(60, 1);

        _rig.Ok(new MoveNotes { Project = _rig.Id, Clip = _clip, Notes = [new NoteTarget(id, 99, 127)] });
        Assert.Equal((NoteRules.LatestStart(4), 127), (Note(id).Start, Note(id).Pitch));
        foreach (NoteTarget target in new[] { new NoteTarget(id, -0.1, 60), new NoteTarget(id, 1, -1), new NoteTarget(id, 1, 128), new NoteTarget(id, double.NaN, 60), new NoteTarget(id, 2_000_000, 60) })
        {
            _rig.RefusedWith(new MoveNotes { Project = _rig.Id, Clip = _clip, Notes = [target] }, CommandErrorCode.Invalid);
        }
    }

    [Fact]
    public void ALengthBelowTheMinimumOrPastTheLimitIsRefused()
    {
        NoteId id = Add(60, 1);

        _rig.RefusedWith(new ResizeNotes { Project = _rig.Id, Clip = _clip, Notes = [new NoteLength(id, 0.01)] }, CommandErrorCode.Invalid);
        _rig.RefusedWith(new ResizeNotes { Project = _rig.Id, Clip = _clip, Notes = [new NoteLength(id, 2_000_000)] }, CommandErrorCode.Invalid);
        _rig.RefusedWith(new ResizeNotes { Project = _rig.Id, Clip = _clip, Notes = [new NoteLength(id, double.NaN)] }, CommandErrorCode.Invalid);
    }

    [Fact]
    public void ADraggedNoteIsOneGestureAndEveryFrameSucceeds()
    {
        NoteId id = Add(60, 1);
        GestureId drag = GestureId.From("note-drag");
        int steps = _rig.History.UndoCount;

        foreach (double start in new[] { 1.1, 1.2, 1.3, 1.4 })
        {
            Assert.True(_rig.Run(new MoveNotes { Project = _rig.Id, Clip = _clip, Notes = [new NoteTarget(id, start, 60)] }, drag).Succeeded);
        }
        _rig.History.EndGesture(ClientId.From("test-client"), drag);

        Assert.Equal(steps + 1, _rig.History.UndoCount);
        Assert.Equal(1.4, Note(id).Start);
        _rig.History.Undo();
        Assert.Equal(1.0, Note(id).Start);
    }

    [Fact]
    public void ADragThatWentPastTheEdgesAndCameBackEndsExactlyOnItsTargetInTimeAndPitchForEveryNote()
    {
        NoteId a = Add(60, 1);
        NoteId b = Add(64, 2);
        GestureId drag = GestureId.From("multi-drag");
        (double, int)[] frames = [(0.5, 70), (3.99, 127), (50, 127), (1.5, 20), (1.25, 61)];

        foreach ((double start, int pitch) in frames)
        {
            // The client keeps the shape: the second note follows one second behind the first.
            NoteTarget[] targets = [new NoteTarget(a, start, pitch), new NoteTarget(b, start + 1, Math.Min(127, pitch + 4))];
            CommandResult result = _rig.Run(new MoveNotes { Project = _rig.Id, Clip = _clip, Notes = [.. targets] }, drag);
            Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(error => error.Message)));
        }
        _rig.History.EndGesture(ClientId.From("test-client"), drag);

        Assert.Equal((1.25, 61), (Note(a).Start, Note(a).Pitch));
        Assert.Equal((2.25, 65), (Note(b).Start, Note(b).Pitch));
    }

    [Fact]
    public void NotesAreResizedToTheirLengthsAndADragThatWentTooShortCanComeBack()
    {
        NoteId a = Add(60, 0, 0.5);

        _rig.Ok(new ResizeNotes { Project = _rig.Id, Clip = _clip, Notes = [new NoteLength(a, 0.75)] });
        Assert.Equal(0.75, Note(a).Duration);
        foreach (double length in new[] { 0.02, 0.5, 0.9 })
        {
            _rig.Ok(new ResizeNotes { Project = _rig.Id, Clip = _clip, Notes = [new NoteLength(a, length)] });
        }

        Assert.Equal(0.9, Note(a).Duration);
    }

    [Fact]
    public void VelocityIsSetForTheChosenNotesWithinOneToOneHundredAndTwentySeven()
    {
        NoteId a = Add(60, 0);
        NoteId b = Add(62, 1);

        _rig.Ok(new SetNoteVelocity { Project = _rig.Id, Clip = _clip, Notes = [b], Velocity = 30 });

        Assert.Equal((100, 30), (Note(a).Velocity, Note(b).Velocity));
        _rig.RefusedWith(new SetNoteVelocity { Project = _rig.Id, Clip = _clip, Velocity = 0 }, CommandErrorCode.Invalid);
        _rig.RefusedWith(new SetNoteVelocity { Project = _rig.Id, Clip = _clip, Velocity = 128 }, CommandErrorCode.Invalid);
    }

    [Fact]
    public void NotesTransposeByOctavesAndSemitonesAndStopAtTheEndsOfThePitchRange()
    {
        NoteId low = Add(5, 0);
        NoteId middle = Add(60, 1);
        NoteId high = Add(125, 2);

        _rig.Ok(new TransposeNotes { Project = _rig.Id, Clip = _clip, Semitones = -12 });
        Assert.Equal([0, 48, 113], new[] { Note(low).Pitch, Note(middle).Pitch, Note(high).Pitch });
        _rig.Ok(new TransposeNotes { Project = _rig.Id, Clip = _clip, Notes = [high], Semitones = 127 });

        Assert.Equal(127, Note(high).Pitch);
        _rig.RefusedWith(new TransposeNotes { Project = _rig.Id, Clip = _clip, Semitones = 128 }, CommandErrorCode.Invalid);
    }

    // ----- quantize

    [Fact]
    public void QuantizeSnapsTheChosenNotesToTheProjectGrid()
    {
        NoteId a = Add(60, 0.13, 0.4);
        NoteId b = Add(62, 1.3, 0.2);

        _rig.Ok(new QuantizeNotes { Project = _rig.Id, Clip = _clip, Notes = [a] });
        Assert.Equal((0.0, 0.5), (Note(a).Start, Note(a).Duration));
        Assert.Equal(1.3, Note(b).Start);
        _rig.Ok(new QuantizeNotes { Project = _rig.Id, Clip = _clip });

        Assert.Equal((1.5, 0.5), (Note(b).Start, Note(b).Duration));
    }

    [Fact]
    public void QuantizeUsesTheProjectGridValueAndTheTempoAtTheClip()
    {
        CommandRig rig = new(_rig.Project with { View = _rig.Project.View with { Grid = "1/8t" } }, 5000);
        NoteId id = new(int.Parse(rig.Created(new AddNote { Project = rig.Id, Clip = _clip, Pitch = 60, Start = 0.1, Duration = 0.2 })[0], System.Globalization.CultureInfo.InvariantCulture));

        rig.Ok(new QuantizeNotes { Project = rig.Id, Clip = _clip });

        MidiNote snapped = rig.Project.Tracks.First(track => track.Id == _keys).MidiClips[0].Notes.First(note => note.Id == id);
        double step = 0.5 / 3;
        Assert.Equal(step, snapped.Start, 9);
        Assert.Equal(step, snapped.Duration, 9);
    }

    [Fact]
    public void QuantizingNotesAlreadyOnTheGridAddsNoUndoStep()
    {
        Add(60, 0.5, 0.5);
        Add(62, 1.0, 1.0);
        int steps = _rig.History.UndoCount;

        _rig.Ok(new QuantizeNotes { Project = _rig.Id, Clip = _clip });

        Assert.Equal(steps, _rig.History.UndoCount);
    }

    // ----- delete

    [Fact]
    public void NotesAreDeletedAndComeBackExactlyOnUndo()
    {
        NoteId a = Add(60, 0);
        NoteId b = Add(62, 1);
        NoteId c = Add(64, 2);
        ProjectDocument before = _rig.Project;

        _rig.Ok(new DeleteNotes { Project = _rig.Id, Clip = _clip, Notes = [a, c] });

        Assert.Equal([b], Clip.Notes.Select(note => note.Id));
        _rig.History.Undo();
        Assert.Equal(before, _rig.Project);
    }

    [Fact]
    public void DeletingNeedsRealNotesAndAtLeastOne()
    {
        Add(60, 0);

        _rig.RefusedWith(new DeleteNotes { Project = _rig.Id, Clip = _clip, Notes = [new NoteId(99)] }, CommandErrorCode.NotFound);
        _rig.RefusedWith(new DeleteNotes { Project = _rig.Id, Clip = _clip, Notes = [] }, CommandErrorCode.Invalid);
        _rig.RefusedWith(new MoveNotes { Project = _rig.Id, Clip = _clip, Notes = [new NoteTarget(new NoteId(99), 1, 60)] }, CommandErrorCode.NotFound);
        _rig.RefusedWith(new MoveNotes { Project = _rig.Id, Clip = _clip, Notes = [new NoteTarget(default, 1, 60)] }, CommandErrorCode.NotFound);
        _rig.RefusedWith(new DeleteNotes { Project = _rig.Id, Clip = _clip, Notes = [default] }, CommandErrorCode.NotFound);
        _rig.RefusedWith(new SetNoteVelocity { Project = _rig.Id, Clip = _clip, Notes = [default], Velocity = 50 }, CommandErrorCode.NotFound);
    }

    [Fact]
    public void EveryNoteCommandRefusesAnUnknownClipAndAnAudioClip()
    {
        ClipId none = ClipId.From("clp_none");
        ClipId audio = ClipId.From(_rig.Created(new AddClip { Project = _rig.Id, Track = _audio, Media = MediaId.From("med_1"), Duration = 3 })[0]);

        foreach ((ClipId clip, CommandErrorCode code) in new[] { (none, CommandErrorCode.NotFound), (audio, CommandErrorCode.Rejected) })
        {
            _rig.RefusedWith(new MoveNotes { Project = _rig.Id, Clip = clip, Notes = [new NoteTarget(new NoteId(1), double.NaN, 60)] }, code);
            _rig.RefusedWith(new ResizeNotes { Project = _rig.Id, Clip = clip, Notes = [new NoteLength(new NoteId(1), 1)] }, code);
            _rig.RefusedWith(new SetNoteVelocity { Project = _rig.Id, Clip = clip, Velocity = 1 }, code);
            _rig.RefusedWith(new TransposeNotes { Project = _rig.Id, Clip = clip, Semitones = 1 }, code);
            _rig.RefusedWith(new QuantizeNotes { Project = _rig.Id, Clip = clip }, code);
            _rig.RefusedWith(new DeleteNotes { Project = _rig.Id, Clip = clip, Notes = [new NoteId(1)] }, code);
            _rig.RefusedWith(new AddControlChange { Project = _rig.Id, Clip = clip, Time = 0, Controller = 64, Value = 127 }, code);
            _rig.RefusedWith(new RemoveControlChanges { Project = _rig.Id, Clip = clip, Time = 0, Controller = 64 }, code);
            _rig.RefusedWith(new ClearControlChanges { Project = _rig.Id, Clip = clip }, code);
        }
    }

    [Fact]
    public void ACommandOnAClipWithNoNotesIsAQuietNoOp()
    {
        int steps = _rig.History.UndoCount;

        _rig.Ok(new TransposeNotes { Project = _rig.Id, Clip = _clip, Semitones = 12 });
        _rig.Ok(new QuantizeNotes { Project = _rig.Id, Clip = _clip });

        Assert.Equal(steps, _rig.History.UndoCount);
    }

    [Fact]
    public void AClipWithAHundredAndThirtyThousandNotesIsEditedWithoutTrouble()
    {
        MidiNote[] notes = [.. Enumerable.Range(1, 130_000).Select(index => new MidiNote(new NoteId(index), 40 + (index % 40), index * 0.007, 0.1, 90))];
        MidiClip big = Clip with { Length = 1000, Notes = [.. notes], NextNoteId = new NoteId(130_001) };
        CommandRig rig = new(_rig.Project with { Tracks = [.. _rig.Project.Tracks.Select(track => track.Id == _keys ? track with { MidiClips = [big] } : track)] }, 9000);

        CommandResult result = rig.Run(new TransposeNotes { Project = rig.Id, Clip = _clip, Semitones = 12 });

        Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(error => error.Message)));
        Assert.Equal(130_000, rig.Project.Tracks.First(track => track.Id == _keys).MidiClips[0].Notes.Count);
    }

    [Fact]
    public void AnEmptyOrOversizedNoteListIsRefusedAndNoListMeansEveryNote()
    {
        NoteId a = Add(60, 0);
        Add(62, 1);

        _rig.RefusedWith(new SetNoteVelocity { Project = _rig.Id, Clip = _clip, Notes = [], Velocity = 50 }, CommandErrorCode.Invalid);
        _rig.RefusedWith(new TransposeNotes { Project = _rig.Id, Clip = _clip, Notes = [], Semitones = 1 }, CommandErrorCode.Invalid);
        _rig.RefusedWith(new QuantizeNotes { Project = _rig.Id, Clip = _clip, Notes = [] }, CommandErrorCode.Invalid);
        _rig.RefusedWith(new MoveNotes { Project = _rig.Id, Clip = _clip, Notes = [] }, CommandErrorCode.Invalid);
        _rig.Ok(new SetNoteVelocity { Project = _rig.Id, Clip = _clip, Velocity = 50 });
        Assert.All(Clip.Notes, note => Assert.Equal(50, note.Velocity));
        Assert.Equal(60, Note(a).Pitch);
    }

    [Theory]
    [InlineData(0.07, 0.01)]
    [InlineData(0.1, 0.01)]
    [InlineData(0.7, 0.1)]
    [InlineData(0.3, 0.1)]
    [InlineData(1.2, 0.01)]
    public void QuantizeNeverPutsANoteAtTheClipEndWhateverTheFloatingPointSays(double length, double step)
    {
        CommandRig rig = new(_rig.Project with { View = _rig.Project.View with { Grid = step == 0.01 ? "10ms" : "0.1s" } }, 5000);
        ClipId clip = ClipId.From(rig.Created(new AddMidiClip { Project = rig.Id, Track = _keys, Start = 10, Length = length })[0]);
        double latest = NoteRules.LatestStart(length);
        foreach (double start in new[] { 0.0, length / 2, latest - 0.004, latest })
        {
            rig.Ok(new AddNote { Project = rig.Id, Clip = clip, Pitch = 60, Start = start });
        }

        CommandResult result = rig.Run(new QuantizeNotes { Project = rig.Id, Clip = clip });

        Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(error => error.Message)));
        MidiClip quantized = rig.Project.Tracks.First(track => track.Id == _keys).MidiClips.First(candidate => candidate.Id == clip);
        Assert.All(quantized.Notes, note => Assert.True(note.Start >= 0 && note.Start <= latest + 1e-12 && note.Start < length));
    }

    [Fact]
    public void QuantizeKeepsEveryNoteInsideItsClipAcrossTemposAndLengths()
    {
        Random random = new(20261122);
        foreach (int bpm in new[] { 60, 73, 97, 120, 133, 150, 173, 200 })
        {
            double sixteenth = 60.0 / bpm / 4;
            for (int k = 2; k <= 64; k++)
            {
                double length = k * sixteenth;
                MidiNote[] notes = [.. Enumerable.Range(1, 12).Select(i => new MidiNote(new NoteId(i), 60, Math.Min(random.NextDouble() * length, NoteRules.LatestStart(length)), 0.1, 100))];
                foreach (double step in new[] { sixteenth, sixteenth * 2, 0.01, 0.1 })
                {
                    foreach (MidiNote note in notes)
                    {
                        MidiNote snapped = NoteRules.Quantize(note, step, length);
                        Assert.True(snapped.Start >= 0 && snapped.Start <= NoteRules.LatestStart(length) && snapped.Start < length, $"{bpm} bpm, {k} sixteenths, step {step}: {snapped.Start} in {length}");
                    }
                }
                MidiClip clip = new()
                {
                    Id = ClipId.From("clp_q"),
                    Name = "q",
                    Start = 0,
                    Length = length,
                    NextNoteId = new NoteId(13),
                    Notes = [.. notes.Select(note => NoteRules.Quantize(note, sixteenth, length))],
                    ControlChanges = [],
                    Muted = false,
                };
                Track track = ProjectDefaults.NewTrack(TrackId.From("trk_q"), "q", TrackKind.Instrument, "#abc", InstrumentPresets.CreateSynth("Soft Keys")) with { MidiClips = [clip] };
                Assert.Empty(ProjectValidator.Validate(BlankProject.Create() with { Tracks = [track] }));
            }
        }
    }

    // ----- controller events

    [Fact]
    public void ControllerEventsAreKeptInTimeOrder()
    {
        _rig.Ok(new AddControlChange { Project = _rig.Id, Clip = _clip, Time = 2, Controller = 64, Value = 127 });
        _rig.Ok(new AddControlChange { Project = _rig.Id, Clip = _clip, Time = 1, Controller = 64, Value = 0 });
        _rig.Ok(new AddControlChange { Project = _rig.Id, Clip = _clip, Time = 2, Controller = 1, Value = 10 });

        Assert.Equal([1.0, 2.0, 2.0], Clip.ControlChanges.Select(change => change.Time));
        Assert.Equal([64, 64, 1], Clip.ControlChanges.Select(change => change.Controller));
    }

    [Fact]
    public void AControllerEventIsInsideTheClipAndInRange()
    {
        _rig.RefusedWith(new AddControlChange { Project = _rig.Id, Clip = _clip, Time = 4.5, Controller = 64, Value = 127 }, CommandErrorCode.Invalid);
        _rig.RefusedWith(new AddControlChange { Project = _rig.Id, Clip = _clip, Time = -1, Controller = 64, Value = 127 }, CommandErrorCode.Invalid);
        _rig.RefusedWith(new AddControlChange { Project = _rig.Id, Clip = _clip, Time = 1, Controller = 128, Value = 127 }, CommandErrorCode.Invalid);
        _rig.RefusedWith(new AddControlChange { Project = _rig.Id, Clip = _clip, Time = 1, Controller = 64, Value = 128 }, CommandErrorCode.Invalid);
        _rig.Ok(new AddControlChange { Project = _rig.Id, Clip = _clip, Time = 4, Controller = 64, Value = 0 });
    }

    [Fact]
    public void RemovingControllerEventsFindsTheClipFirstThenChecksTheControllerAndTheTime()
    {
        _rig.RefusedWith(new RemoveControlChanges { Project = _rig.Id, Clip = ClipId.From("clp_none"), Time = 0, Controller = 200 }, CommandErrorCode.NotFound);
        _rig.RefusedWith(new RemoveControlChanges { Project = _rig.Id, Clip = _clip, Time = 0, Controller = 200 }, CommandErrorCode.Invalid);
        _rig.RefusedWith(new RemoveControlChanges { Project = _rig.Id, Clip = _clip, Time = -1, Controller = 64 }, CommandErrorCode.Invalid);
        _rig.RefusedWith(new RemoveControlChanges { Project = _rig.Id, Clip = _clip, Time = 0, Controller = 64 }, CommandErrorCode.NotFound);
    }

    [Fact]
    public void ControllerEventsAreRemovedByTimeAndControllerOrClearedByControllerOrAll()
    {
        _rig.Ok(new AddControlChange { Project = _rig.Id, Clip = _clip, Time = 1, Controller = 64, Value = 127 });
        _rig.Ok(new AddControlChange { Project = _rig.Id, Clip = _clip, Time = 2, Controller = 64, Value = 0 });
        _rig.Ok(new AddControlChange { Project = _rig.Id, Clip = _clip, Time = 2, Controller = 1, Value = 5 });

        _rig.Ok(new RemoveControlChanges { Project = _rig.Id, Clip = _clip, Time = 2, Controller = 64 });
        Assert.Equal(2, Clip.ControlChanges.Count);
        _rig.RefusedWith(new RemoveControlChanges { Project = _rig.Id, Clip = _clip, Time = 2, Controller = 64 }, CommandErrorCode.NotFound);
        _rig.Ok(new ClearControlChanges { Project = _rig.Id, Clip = _clip, Controller = 1 });
        Assert.Equal([64], Clip.ControlChanges.Select(change => change.Controller));
        _rig.Ok(new ClearControlChanges { Project = _rig.Id, Clip = _clip });

        Assert.Empty(Clip.ControlChanges);
        int steps = _rig.History.UndoCount;
        _rig.Ok(new ClearControlChanges { Project = _rig.Id, Clip = _clip });
        Assert.Equal(steps, _rig.History.UndoCount);
    }
}
