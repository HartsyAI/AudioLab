using AudioLab.Core.Commands;
using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Core.Tests;

public sealed class ClipCommandTests
{
    private static readonly MediaId _media = MediaId.From("med_1");

    private readonly TrackId _a = TrackId.From("trk_a");

    private readonly TrackId _b = TrackId.From("trk_b");

    private readonly TrackId _keys = TrackId.From("trk_keys");

    private readonly TrackId _bus = TrackId.From("trk_bus");

    private readonly CommandRig _rig;

    public ClipCommandTests()
    {
        Track keys = ProjectDefaults.NewTrack(_keys, "Keys", TrackKind.Instrument, "#abc", InstrumentPresets.CreateSynth("Soft Keys"));
        ProjectDocument start = BlankProject.Create() with
        {
            Tracks = [BlankProject.Track("trk_a", "A"), BlankProject.Track("trk_b", "B"), keys, BlankProject.Track("trk_bus", "Bus", TrackKind.Bus)],
        };
        _rig = new CommandRig(start);
    }

    private Track TrackOf(TrackId id) => _rig.Project.Tracks.First(track => track.Id == id);

    private ClipId AddAudio(TrackId track, double duration = 10, double? start = null, string? name = null)
    {
        return ClipId.From(_rig.Created(new AddClip { Project = _rig.Id, Track = track, Media = _media, Duration = duration, Start = start, Name = name })[0]);
    }

    private ClipId AddMidi(double start = 0, double length = 4)
    {
        return ClipId.From(_rig.Created(new AddMidiClip { Project = _rig.Id, Track = _keys, Start = start, Length = length })[0]);
    }

    private AudioClip Audio(ClipId id) => _rig.Project.Tracks.SelectMany(track => track.Clips).First(clip => clip.Id == id);

    private MidiClip Midi(ClipId id) => _rig.Project.Tracks.SelectMany(track => track.MidiClips).First(clip => clip.Id == id);

    // ----- add

    [Fact]
    public void AnAudioClipIsAddedAtTheEndOfTheTrackWithADefaultNameAndAServerAllocatedId()
    {
        ClipId first = AddAudio(_a, 10);
        ClipId second = AddAudio(_a, 5);

        Assert.Equal("clp_1001", first.Value);
        Assert.Equal((0.0, "Clip 1"), (Audio(first).Start, Audio(first).Name));
        Assert.Equal((10.0, "Clip 2"), (Audio(second).Start, Audio(second).Name));
        AudioClip clip = Audio(second);
        Assert.Equal((_media, 5.0, 0.0, 0.0, 1.0, 0.0, 0.0, false), (clip.Media, clip.Duration, clip.Offset, clip.TrimEnd, clip.Gain, clip.FadeIn, clip.FadeOut, clip.Muted));
        Assert.Null(clip.Color);
        Assert.Null(clip.Meta);
    }

    [Fact]
    public void AnAudioClipTakesItsPositionNameAndColourAsGiven()
    {
        ClipId id = ClipId.From(_rig.Created(new AddClip { Project = _rig.Id, Track = _a, Media = _media, Duration = 3, Start = 7.5, Name = " Intro ", Color = "#123456" })[0]);

        Assert.Equal((7.5, "Intro", "#123456"), (Audio(id).Start, Audio(id).Name, Audio(id).Color));
    }

    [Fact]
    public void AnAudioClipGoesOnlyOnAnAudioTrackAndNeedsARealDuration()
    {
        _rig.RefusedWith(new AddClip { Project = _rig.Id, Track = _keys, Media = _media, Duration = 3 }, CommandErrorCode.Rejected);
        _rig.RefusedWith(new AddClip { Project = _rig.Id, Track = _bus, Media = _media, Duration = 3 }, CommandErrorCode.Rejected);
        _rig.RefusedWith(new AddClip { Project = _rig.Id, Track = TrackId.From("trk_none"), Media = _media, Duration = 3 }, CommandErrorCode.NotFound);
        foreach (double duration in new[] { 0, -1, double.NaN, double.PositiveInfinity, 2_000_000 })
        {
            _rig.RefusedWith(new AddClip { Project = _rig.Id, Track = _a, Media = _media, Duration = duration }, CommandErrorCode.Invalid);
        }
        _rig.RefusedWith(new AddClip { Project = _rig.Id, Track = _a, Media = _media, Duration = 3, Start = -1 }, CommandErrorCode.Invalid);
        _rig.RefusedWith(new AddClip { Project = _rig.Id, Track = _a, Media = _media, Duration = 3, Name = new string('n', 201) }, CommandErrorCode.Invalid);
        _rig.RefusedWith(new AddClip { Project = _rig.Id, Track = _a, Media = _media, Duration = 3, Color = "red" }, CommandErrorCode.Invalid);
    }

    [Fact]
    public void AMidiClipIsAddedEmptyToAnInstrumentTrackOnly()
    {
        ClipId id = AddMidi(2, 4);

        MidiClip clip = Midi(id);
        Assert.Equal(("MIDI 1", 2.0, 4.0), (clip.Name, clip.Start, clip.Length));
        Assert.Empty(clip.Notes);
        Assert.Equal(NoteId.First, clip.NextNoteId);
        _rig.RefusedWith(new AddMidiClip { Project = _rig.Id, Track = _a, Start = 0, Length = 1 }, CommandErrorCode.Rejected);
        _rig.RefusedWith(new AddMidiClip { Project = _rig.Id, Track = _keys, Start = 0, Length = 0.01 }, CommandErrorCode.Invalid);
        _rig.RefusedWith(new AddMidiClip { Project = _rig.Id, Track = _keys, Start = -1, Length = 1 }, CommandErrorCode.Invalid);
        _rig.RefusedWith(new AddMidiClip { Project = _rig.Id, Track = TrackId.From("trk_none"), Start = 0, Length = 1 }, CommandErrorCode.NotFound);
    }

    // ----- move

    [Fact]
    public void AClipMovesInTimeAndStopsAtTheStartOfTheProject()
    {
        ClipId id = AddAudio(_a, start: 5);

        _rig.Ok(new MoveClip { Project = _rig.Id, Clip = id, Start = 12 });
        Assert.Equal(12, Audio(id).Start);
        _rig.Ok(new MoveClip { Project = _rig.Id, Clip = id, Start = -3 });

        Assert.Equal(0, Audio(id).Start);
    }

    [Fact]
    public void MovingAClipByNothingAddsNoUndoStep()
    {
        ClipId id = AddAudio(_a, start: 5);
        int steps = _rig.History.UndoCount;

        _rig.Ok(new MoveClip { Project = _rig.Id, Clip = id, Start = 5 });

        Assert.Equal(steps, _rig.History.UndoCount);
    }

    [Fact]
    public void AClipMovedToAnotherTrackLandsOverTheClipsThereAndKeepsEverythingElse()
    {
        ClipId moved = AddAudio(_a, name: "Moved");
        ClipId resident = AddAudio(_b, name: "Resident");
        AudioClip before = Audio(moved);

        _rig.Ok(new MoveClip { Project = _rig.Id, Clip = moved, Start = 4, Track = _b });

        Assert.Empty(TrackOf(_a).Clips);
        Assert.Equal([resident, moved], TrackOf(_b).Clips.Select(clip => clip.Id));
        Assert.Equal(before with { Start = 4 }, Audio(moved));
        _rig.History.Undo();
        Assert.Equal([moved], TrackOf(_a).Clips.Select(clip => clip.Id));
    }

    [Fact]
    public void AClipMovesOnlyToATrackOfItsKind()
    {
        ClipId audio = AddAudio(_a);
        ClipId midi = AddMidi();

        _rig.RefusedWith(new MoveClip { Project = _rig.Id, Clip = audio, Start = 1, Track = _keys }, CommandErrorCode.Rejected);
        _rig.RefusedWith(new MoveClip { Project = _rig.Id, Clip = audio, Start = 1, Track = _bus }, CommandErrorCode.Rejected);
        _rig.RefusedWith(new MoveClip { Project = _rig.Id, Clip = midi, Start = 1, Track = _a }, CommandErrorCode.Rejected);
        _rig.RefusedWith(new MoveClip { Project = _rig.Id, Clip = audio, Start = 1, Track = TrackId.From("trk_none") }, CommandErrorCode.NotFound);
    }

    [Fact]
    public void AMidiClipMovesInTime()
    {
        ClipId id = AddMidi(1, 4);

        _rig.Ok(new MoveClip { Project = _rig.Id, Clip = id, Start = 6 });

        Assert.Equal(6, Midi(id).Start);
    }

    [Fact]
    public void ADraggedClipIsOneGestureAndEveryFrameSucceeds()
    {
        ClipId id = AddAudio(_a, start: 2);
        GestureId drag = GestureId.From("clip-drag");
        int steps = _rig.History.UndoCount;

        foreach (double start in new[] { 3.0, 4.5, 8.0, 11.0 })
        {
            Assert.True(_rig.Run(new MoveClip { Project = _rig.Id, Clip = id, Start = start }, drag).Succeeded, $"at {start}");
        }
        _rig.History.EndGesture(ClientId.From("test-client"), drag);

        Assert.Equal(steps + 1, _rig.History.UndoCount);
        _rig.History.Undo();
        Assert.Equal(2, Audio(id).Start);
    }

    // ----- trim

    [Fact]
    public void TheStartEdgeIsTrimmedAndTheAudioAfterItStaysPut()
    {
        ClipId id = AddAudio(_a, 20, start: 10);

        _rig.Ok(new TrimClip { Project = _rig.Id, Clip = id, Edge = ClipEdge.Start, Time = 14 });

        Assert.Equal((14.0, 4.0, 0.0), (Audio(id).Start, Audio(id).Offset, Audio(id).TrimEnd));
    }

    [Fact]
    public void TheEndEdgeIsTrimmedInAndOut()
    {
        ClipId id = AddAudio(_a, 20, start: 10);

        _rig.Ok(new TrimClip { Project = _rig.Id, Clip = id, Edge = ClipEdge.End, Time = 25 });
        Assert.Equal(5, Audio(id).TrimEnd);
        _rig.Ok(new TrimClip { Project = _rig.Id, Clip = id, Edge = ClipEdge.End, Time = 99 });

        Assert.Equal(0, Audio(id).TrimEnd);
    }

    [Fact]
    public void ATrimDragIsOneGesture()
    {
        ClipId id = AddAudio(_a, 20, start: 10);
        GestureId drag = GestureId.From("trim-drag");
        int steps = _rig.History.UndoCount;

        foreach (double time in new[] { 11.0, 12.0, 13.0, 15.0 })
        {
            Assert.True(_rig.Run(new TrimClip { Project = _rig.Id, Clip = id, Edge = ClipEdge.Start, Time = time }, drag).Succeeded, $"at {time}");
        }
        _rig.History.EndGesture(ClientId.From("test-client"), drag);

        Assert.Equal(steps + 1, _rig.History.UndoCount);
        Assert.Equal(15, Audio(id).Start);
        _rig.History.Undo();
        Assert.Equal((10.0, 0.0), (Audio(id).Start, Audio(id).Offset));
    }

    [Fact]
    public void ATrimKeepsTheClipValidWhateverTheFadesWere()
    {
        ClipId id = AddAudio(_a, 20);
        _rig.Ok(new SetClipFades { Project = _rig.Id, Clip = id, FadeIn = 8, FadeOut = 8 });

        _rig.Ok(new TrimClip { Project = _rig.Id, Clip = id, Edge = ClipEdge.End, Time = 2 });

        Assert.Equal((2.0, 2.0), (Audio(id).FadeIn, Audio(id).FadeOut));
    }

    [Fact]
    public void OnlyAnAudioClipIsTrimmedAndAMissingClipIsNotFoundBeforeTheTimeIsChecked()
    {
        ClipId midi = AddMidi();

        _rig.RefusedWith(new TrimClip { Project = _rig.Id, Clip = midi, Edge = ClipEdge.End, Time = 2 }, CommandErrorCode.Rejected);
        _rig.RefusedWith(new TrimClip { Project = _rig.Id, Clip = ClipId.From("clp_none"), Edge = ClipEdge.End, Time = double.NaN }, CommandErrorCode.NotFound);
        _rig.RefusedWith(new TrimClip { Project = _rig.Id, Clip = AddAudio(_a), Edge = ClipEdge.End, Time = double.NaN }, CommandErrorCode.Invalid);
    }

    // ----- split

    [Fact]
    public void ASplitLeavesTwoClipsThatKeepTheOrderTheIdsAndUndoExactly()
    {
        ClipId under = AddAudio(_a, name: "Under");
        ClipId id = AddAudio(_a, 20, start: 10, name: "Take");
        ClipId over = AddAudio(_a, name: "Over");
        ProjectDocument before = _rig.Project;

        IReadOnlyList<string> created = _rig.Created(new SplitClip { Project = _rig.Id, Clip = id, At = 14 });

        ClipId right = ClipId.From(Assert.Single(created));
        Assert.Equal([under, id, right, over], TrackOf(_a).Clips.Select(clip => clip.Id));
        Assert.Equal((10.0, 14.0), (Audio(id).Start, ClipRules.EndOf(Audio(id))));
        Assert.Equal((14.0, 30.0), (Audio(right).Start, ClipRules.EndOf(Audio(right))));
        Assert.Equal("Take", Audio(right).Name);
        _rig.History.Undo();
        Assert.Equal(before, _rig.Project);
        _rig.History.Redo();
        Assert.Equal(right, TrackOf(_a).Clips[2].Id);
    }

    [Fact]
    public void ASplitNeedsAPointInsideTheClipAndAnUnusableOneKeepsTheRedoSteps()
    {
        ClipId id = AddAudio(_a, 10, start: 0);
        ClipId midi = AddMidi();
        _rig.Ok(new SetClipGain { Project = _rig.Id, Clip = id, Gain = 0.5 });
        _rig.History.Undo();
        Assert.Equal(1, _rig.History.RedoCount);

        _rig.RefusedWith(new SplitClip { Project = _rig.Id, Clip = id, At = 0 }, CommandErrorCode.Rejected);
        _rig.RefusedWith(new SplitClip { Project = _rig.Id, Clip = id, At = 10 }, CommandErrorCode.Rejected);
        _rig.RefusedWith(new SplitClip { Project = _rig.Id, Clip = id, At = 99 }, CommandErrorCode.Rejected);
        _rig.RefusedWith(new SplitClip { Project = _rig.Id, Clip = id, At = double.NaN }, CommandErrorCode.Invalid);
        _rig.RefusedWith(new SplitClip { Project = _rig.Id, Clip = ClipId.From("clp_none"), At = 4 }, CommandErrorCode.NotFound);
        _rig.RefusedWith(new SplitClip { Project = _rig.Id, Clip = midi, At = 2 }, CommandErrorCode.Rejected);

        Assert.Equal(1, _rig.History.RedoCount);
    }

    [Fact]
    public void ASplitCanBeSplitAgainAndTheRightPieceCarriesLyricsButNotTheScore()
    {
        ClipLyrics lyrics = new() { Text = "la", Words = [new LyricWord("la", 0.5, 0.9)] };
        ClipMeta meta = new() { Score = new ScoreMeta { Abc = "X:1" }, Lyrics = lyrics };
        ClipId id = AddAudio(_a, 20);
        ProjectDocument withMeta = _rig.Project with
        {
            Tracks = [.. _rig.Project.Tracks.Select(track => track.Id == _a ? track with { Clips = [track.Clips[0] with { Meta = meta }] } : track)],
        };
        CommandRig rig = new(withMeta, 5000);

        ClipId right = ClipId.From(rig.Created(new SplitClip { Project = rig.Id, Clip = id, At = 5 })[0]);
        rig.Ok(new SplitClip { Project = rig.Id, Clip = right, At = 12 });

        AudioClip[] clips = [.. rig.Project.Tracks.First(track => track.Id == _a).Clips];
        Assert.Equal(3, clips.Length);
        Assert.Equal(meta, clips[0].Meta);
        Assert.Equal(lyrics, clips[1].Meta!.Lyrics);
        Assert.Null(clips[1].Meta!.Score);
    }

    // ----- duplicate

    [Fact]
    public void ACopyStartsHalfASecondAfterTheOriginalAndKeepsItsSettings()
    {
        ClipId id = AddAudio(_a, 10, start: 2, name: "Take");
        _rig.Ok(new SetClipMuted { Project = _rig.Id, Clip = id, Muted = true });
        _rig.Ok(new SetClipColor { Project = _rig.Id, Clip = id, Color = "#abc" });
        _rig.Ok(new SetClipGain { Project = _rig.Id, Clip = id, Gain = 0.6 });
        _rig.Ok(new TrimClip { Project = _rig.Id, Clip = id, Edge = ClipEdge.End, Time = 9 });
        ProjectDocument before = _rig.Project;

        ClipId copy = ClipId.From(_rig.Created(new DuplicateClip { Project = _rig.Id, Clip = id })[0]);

        AudioClip original = Audio(id);
        AudioClip duplicate = Audio(copy);
        Assert.Equal(before.Tracks[0].Clips[0], original);
        Assert.Equal((ClipRules.EndOf(original) + 0.5, "Take (copy)", true, "#abc", 0.6), (duplicate.Start, duplicate.Name, duplicate.Muted, duplicate.Color, duplicate.Gain));
        Assert.Equal((original.Media, original.Duration, original.Offset, original.TrimEnd), (duplicate.Media, duplicate.Duration, duplicate.Offset, duplicate.TrimEnd));
        Assert.Equal(copy, TrackOf(_a).Clips[^1].Id);
        _rig.History.Undo();
        Assert.Equal(before, _rig.Project);
    }

    [Fact]
    public void ACopyOfACopyIsNumberedAndACopyCarriesOnlyTheLyrics()
    {
        ClipId id = AddAudio(_a, 4, name: "Take");
        ClipLyrics lyrics = new() { Text = "la", Words = [new LyricWord("la", 0.5, 0.9)] };
        ProjectDocument withMeta = _rig.Project with
        {
            Tracks = [.. _rig.Project.Tracks.Select(track => track.Id == _a ? track with { Clips = [track.Clips[0] with { Meta = new ClipMeta { Score = new ScoreMeta { Abc = "X:1" }, Lyrics = lyrics } }] } : track)],
        };
        CommandRig rig = new(withMeta, 5000);

        ClipId first = ClipId.From(rig.Created(new DuplicateClip { Project = rig.Id, Clip = id })[0]);
        ClipId second = ClipId.From(rig.Created(new DuplicateClip { Project = rig.Id, Clip = first, Start = 30 })[0]);

        AudioClip[] clips = [.. rig.Project.Tracks.First(track => track.Id == _a).Clips];
        Assert.Equal(["Take", "Take (copy)", "Take (copy 2)"], clips.Select(clip => clip.Name));
        Assert.Equal(30, clips[2].Start);
        Assert.Null(clips[1].Meta!.Score);
        Assert.Equal(lyrics, clips[2].Meta!.Lyrics);
        Assert.Equal(second, clips[2].Id);
    }

    [Fact]
    public void ACopyOfAMidiClipKeepsItsNotesColourAndMute()
    {
        ClipId id = AddMidi(0, 4);
        MidiClip withNotes = Midi(id) with { Notes = [new MidiNote(NoteId.First, 60, 0.5, 1, 100)], NextNoteId = NoteId.First.Next(), Color = "#abc", Muted = true };
        ProjectDocument document = _rig.Project with
        {
            Tracks = [.. _rig.Project.Tracks.Select(track => track.Id == _keys ? track with { MidiClips = [withNotes] } : track)],
        };
        CommandRig rig = new(document, 5000);

        ClipId copy = ClipId.From(rig.Created(new DuplicateClip { Project = rig.Id, Clip = id })[0]);

        MidiClip duplicate = rig.Project.Tracks.First(track => track.Id == _keys).MidiClips.First(clip => clip.Id == copy);
        Assert.Equal((4.5, "MIDI 1 (copy)", true, "#abc"), (duplicate.Start, duplicate.Name, duplicate.Muted, duplicate.Color));
        Assert.Equal(withNotes.Notes, duplicate.Notes);
        Assert.Equal(withNotes.NextNoteId, duplicate.NextNoteId);
    }

    [Fact]
    public void ACopyNeedsARealClipAndAStartInRange()
    {
        ClipId id = AddAudio(_a);

        _rig.RefusedWith(new DuplicateClip { Project = _rig.Id, Clip = ClipId.From("clp_none") }, CommandErrorCode.NotFound);
        _rig.RefusedWith(new DuplicateClip { Project = _rig.Id, Clip = id, Start = -1 }, CommandErrorCode.Invalid);
        _rig.RefusedWith(new DuplicateClip { Project = _rig.Id, Clip = id, Start = double.NaN }, CommandErrorCode.Invalid);
    }

    // ----- delete

    [Fact]
    public void ADeletedClipComesBackWithItsIdAndPlaceOnUndo()
    {
        ClipId first = AddAudio(_a);
        ClipId second = AddAudio(_a);
        ProjectDocument before = _rig.Project;

        _rig.Ok(new DeleteClip { Project = _rig.Id, Clip = first });

        Assert.Equal([second], TrackOf(_a).Clips.Select(clip => clip.Id));
        _rig.History.Undo();
        Assert.Equal(before, _rig.Project);
    }

    [Fact]
    public void DeletingInSlipOrGridModeLeavesAGap()
    {
        ClipId first = AddAudio(_a, 10, start: 0);
        ClipId second = AddAudio(_a, 10, start: 10);

        _rig.Ok(new DeleteClip { Project = _rig.Id, Clip = first });

        Assert.Equal(10, Audio(second).Start);
    }

    [Fact]
    public void DeletingInShuffleModeClosesTheGapOnTheTrackOnly()
    {
        CommandRig rig = new(_rig.Project with { View = _rig.Project.View with { EditMode = EditMode.Shuffle } });
        ClipId first = ClipId.From(rig.Created(new AddClip { Project = rig.Id, Track = _a, Media = _media, Duration = 6, Start = 0 })[0]);
        ClipId second = ClipId.From(rig.Created(new AddClip { Project = rig.Id, Track = _a, Media = _media, Duration = 6, Start = 6 })[0]);
        ClipId third = ClipId.From(rig.Created(new AddClip { Project = rig.Id, Track = _a, Media = _media, Duration = 6, Start = 20 })[0]);
        ClipId other = ClipId.From(rig.Created(new AddClip { Project = rig.Id, Track = _b, Media = _media, Duration = 6, Start = 8 })[0]);

        rig.Ok(new DeleteClip { Project = rig.Id, Clip = first });

        AudioClip[] clips = [.. rig.Project.Tracks.First(track => track.Id == _a).Clips];
        Assert.Equal([second, third], clips.Select(clip => clip.Id));
        Assert.Equal([0.0, 14.0], clips.Select(clip => clip.Start));
        Assert.Equal(8, rig.Project.Tracks.First(track => track.Id == _b).Clips.First(clip => clip.Id == other).Start);
    }

    [Fact]
    public void DeletingAMidiClipInShuffleModeSlidesTheLaterMidiClipsLeft()
    {
        CommandRig rig = new(_rig.Project with { View = _rig.Project.View with { EditMode = EditMode.Shuffle } });
        ClipId first = ClipId.From(rig.Created(new AddMidiClip { Project = rig.Id, Track = _keys, Start = 0, Length = 4 })[0]);
        ClipId second = ClipId.From(rig.Created(new AddMidiClip { Project = rig.Id, Track = _keys, Start = 4, Length = 2 })[0]);
        ClipId third = ClipId.From(rig.Created(new AddMidiClip { Project = rig.Id, Track = _keys, Start = 10, Length = 2 })[0]);

        rig.Ok(new DeleteClip { Project = rig.Id, Clip = first });

        MidiClip[] clips = [.. rig.Project.Tracks.First(track => track.Id == _keys).MidiClips];
        Assert.Equal([second, third], clips.Select(clip => clip.Id));
        Assert.Equal([0.0, 6.0], clips.Select(clip => clip.Start));
    }

    [Fact]
    public void DeletingAClipClearsTheLinksOtherClipsHadToIt()
    {
        ClipId source = AddAudio(_a, name: "Source");
        AddAudio(_b, name: "Stem");
        ProjectDocument linked = _rig.Project with
        {
            Tracks = [.. _rig.Project.Tracks.Select(track => track.Id == _b ? track with { Clips = [track.Clips[0] with { Meta = new ClipMeta { StemOf = new StemOf(source, "vocals") } }] } : track)],
        };
        CommandRig rig = new(linked, 5000);

        rig.Ok(new DeleteClip { Project = rig.Id, Clip = source });

        Assert.Null(rig.Project.Tracks.First(track => track.Id == _b).Clips[0].Meta!.StemOf!.Value.ClipId);
    }

    // ----- gain, fades, length

    [Fact]
    public void GainIsSetWithinItsRangeAndRefusedOutsideIt()
    {
        ClipId id = AddAudio(_a);

        _rig.Ok(new SetClipGain { Project = _rig.Id, Clip = id, Gain = 2 });
        Assert.Equal(2, Audio(id).Gain);
        foreach (double gain in new[] { -0.1, 16.1, double.NaN })
        {
            _rig.RefusedWith(new SetClipGain { Project = _rig.Id, Clip = id, Gain = gain }, CommandErrorCode.Invalid);
        }
        _rig.RefusedWith(new SetClipGain { Project = _rig.Id, Clip = ClipId.From("clp_none"), Gain = double.NaN }, CommandErrorCode.NotFound);
        _rig.RefusedWith(new SetClipGain { Project = _rig.Id, Clip = AddMidi(), Gain = 1 }, CommandErrorCode.Rejected);
    }

    [Fact]
    public void FadesAreKeptToWhatIsLeftOfTheClipAndAFadeDragIsOneGesture()
    {
        ClipId id = AddAudio(_a, 10);
        GestureId drag = GestureId.From("fade-drag");
        int steps = _rig.History.UndoCount;

        foreach (double fade in new[] { 1.0, 3.0, 6.0, 50.0 })
        {
            Assert.True(_rig.Run(new SetClipFades { Project = _rig.Id, Clip = id, FadeIn = fade }, drag).Succeeded);
        }
        _rig.History.EndGesture(ClientId.From("test-client"), drag);

        Assert.Equal(steps + 1, _rig.History.UndoCount);
        Assert.Equal(10, Audio(id).FadeIn);
        _rig.Ok(new SetClipFades { Project = _rig.Id, Clip = id, FadeOut = 4 });
        Assert.Equal((10.0, 0.0), (Audio(id).FadeIn, Audio(id).FadeOut));
        _rig.RefusedWith(new SetClipFades { Project = _rig.Id, Clip = id, FadeIn = double.NaN }, CommandErrorCode.Invalid);
    }

    [Fact]
    public void AMidiClipLengthGrowsAndShrinksButNotPastItsNotes()
    {
        ClipId id = AddMidi(0, 4);
        ProjectDocument withNote = _rig.Project with
        {
            Tracks = [.. _rig.Project.Tracks.Select(track => track.Id == _keys ? track with { MidiClips = [track.MidiClips[0] with { Notes = [new MidiNote(NoteId.First, 60, 3, 0.5, 100)], NextNoteId = NoteId.First.Next() }] } : track)],
        };
        CommandRig rig = new(withNote, 5000);

        rig.Ok(new SetMidiClipLength { Project = rig.Id, Clip = id, Length = 8 });
        Assert.Equal(8, rig.Project.Tracks.First(track => track.Id == _keys).MidiClips[0].Length);
        rig.RefusedWith(new SetMidiClipLength { Project = rig.Id, Clip = id, Length = 2 }, CommandErrorCode.Invalid);
        rig.RefusedWith(new SetMidiClipLength { Project = rig.Id, Clip = id, Length = double.NaN }, CommandErrorCode.Invalid);
    }

    [Fact]
    public void AMidiLengthBelowTheMinimumBecomesTheMinimumAndOnlyAMidiClipTakesIt()
    {
        ClipId id = AddMidi(0, 4);

        _rig.Ok(new SetMidiClipLength { Project = _rig.Id, Clip = id, Length = 0.001 });

        Assert.Equal(MidiClip.MinLength, Midi(id).Length);
        _rig.RefusedWith(new SetMidiClipLength { Project = _rig.Id, Clip = AddAudio(_a), Length = 1 }, CommandErrorCode.Rejected);
        _rig.RefusedWith(new SetMidiClipLength { Project = _rig.Id, Clip = ClipId.From("clp_none"), Length = 1 }, CommandErrorCode.NotFound);
    }

    // ----- mute, name, colour, order

    [Fact]
    public void MuteNameAndColourApplyToAudioAndMidiClips()
    {
        ClipId audio = AddAudio(_a);
        ClipId midi = AddMidi();

        foreach (ClipId id in new[] { audio, midi })
        {
            _rig.Ok(new SetClipMuted { Project = _rig.Id, Clip = id, Muted = true });
            _rig.Ok(new RenameClip { Project = _rig.Id, Clip = id, Name = "  Chorus " });
            _rig.Ok(new SetClipColor { Project = _rig.Id, Clip = id, Color = "#abc" });
        }

        Assert.Equal((true, "Chorus", "#abc"), (Audio(audio).Muted, Audio(audio).Name, Audio(audio).Color));
        Assert.Equal((true, "Chorus", "#abc"), (Midi(midi).Muted, Midi(midi).Name, Midi(midi).Color));
        _rig.Ok(new SetClipColor { Project = _rig.Id, Clip = audio });
        Assert.Null(Audio(audio).Color);
    }

    [Fact]
    public void ANameMayBeEmptyButNotLongerThanTheSchemaAllowsAndAColourMustBeOne()
    {
        ClipId id = AddAudio(_a);

        _rig.Ok(new RenameClip { Project = _rig.Id, Clip = id, Name = "" });
        Assert.Equal(string.Empty, Audio(id).Name);
        _rig.RefusedWith(new RenameClip { Project = _rig.Id, Clip = id, Name = new string('n', 201) }, CommandErrorCode.Invalid);
        _rig.RefusedWith(new SetClipColor { Project = _rig.Id, Clip = id, Color = "blue" }, CommandErrorCode.Invalid);
    }

    [Fact]
    public void AClipIsBroughtToTheFrontAndSentToTheBackOfItsTrack()
    {
        ClipId one = AddAudio(_a);
        ClipId two = AddAudio(_a);
        ClipId three = AddAudio(_a);

        _rig.Ok(new ReorderClip { Project = _rig.Id, Clip = one, Layer = ClipLayer.Front });
        Assert.Equal([two, three, one], TrackOf(_a).Clips.Select(clip => clip.Id));
        _rig.Ok(new ReorderClip { Project = _rig.Id, Clip = three, Layer = ClipLayer.Back });
        Assert.Equal([three, two, one], TrackOf(_a).Clips.Select(clip => clip.Id));
        int steps = _rig.History.UndoCount;
        _rig.Ok(new ReorderClip { Project = _rig.Id, Clip = one, Layer = ClipLayer.Front });
        Assert.Equal(steps, _rig.History.UndoCount);
    }

    [Fact]
    public void AMidiClipIsReorderedToo()
    {
        ClipId one = AddMidi(0, 2);
        ClipId two = AddMidi(4, 2);

        _rig.Ok(new ReorderClip { Project = _rig.Id, Clip = two, Layer = ClipLayer.Back });

        Assert.Equal([two, one], TrackOf(_keys).MidiClips.Select(clip => clip.Id));
    }

    [Fact]
    public void AnUnknownClipIsNotFoundByEveryCommand()
    {
        ClipId none = ClipId.From("clp_none");

        _rig.RefusedWith(new MoveClip { Project = _rig.Id, Clip = none, Start = 1 }, CommandErrorCode.NotFound);
        _rig.RefusedWith(new DeleteClip { Project = _rig.Id, Clip = none }, CommandErrorCode.NotFound);
        _rig.RefusedWith(new SetClipMuted { Project = _rig.Id, Clip = none, Muted = true }, CommandErrorCode.NotFound);
        _rig.RefusedWith(new RenameClip { Project = _rig.Id, Clip = none, Name = "x" }, CommandErrorCode.NotFound);
        _rig.RefusedWith(new SetClipColor { Project = _rig.Id, Clip = none }, CommandErrorCode.NotFound);
        _rig.RefusedWith(new ReorderClip { Project = _rig.Id, Clip = none, Layer = ClipLayer.Front }, CommandErrorCode.NotFound);
        _rig.RefusedWith(new SetClipFades { Project = _rig.Id, Clip = none, FadeIn = double.NaN }, CommandErrorCode.NotFound);
    }

    [Fact]
    public void ARedoneAddKeepsItsId()
    {
        ClipId id = AddAudio(_a);

        _rig.History.Undo();
        _rig.History.Redo();

        Assert.Equal(id, TrackOf(_a).Clips[0].Id);
    }
}
