using AudioLab.Core.Commands;
using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Core.Tests;

// The domain commands against a project that uses every part of the model, not an empty one.
public sealed class RichProjectTests
{
    private static ProjectDocument Sample() => SampleProject.Create(UserId.Local);

    private static AudioClip Clip(string id, ClipMeta? meta = null)
    {
        return new AudioClip
        {
            Id = ClipId.From(id),
            Name = id,
            Media = MediaId.From("med_1"),
            Start = 0,
            Duration = 10,
            Offset = 0,
            TrimEnd = 0,
            Gain = 1,
            FadeIn = 0,
            FadeOut = 0,
            Muted = false,
            Meta = meta,
        };
    }

    [Fact]
    public void EveryGroupTrackAndMixerCommandRunsAgainstTheSampleProject()
    {
        ProjectDocument sample = Sample();
        Track vocals = sample.Tracks.First(track => track.Sends.Count > 0);
        Track keys = sample.Tracks.First(track => track.Kind == TrackKind.Instrument);
        Track bus = sample.Tracks.First(track => track.Kind == TrackKind.Bus);
        Track guitar = sample.Tracks.First(track => track.Group is not null);
        GroupId band = sample.Groups[0].Id;
        ProjectId id = sample.Id;
        Command[] commands =
        [
            new CreateGroup { Project = id, Tracks = [vocals.Id, keys.Id] },
            new AssignToGroup { Project = id, Tracks = [vocals.Id], Group = band },
            new AssignToGroup { Project = id, Tracks = [guitar.Id] },
            new Ungroup { Project = id, Group = band },
            new RenameGroup { Project = id, Group = band, Name = "Rhythm" },
            new SetGroupColor { Project = id, Group = band, Color = "#123456" },
            new SetGroupCollapsed { Project = id, Group = band, Collapsed = false },
            new SetGroupTint { Project = id, Group = band, Tint = true },
            new SetGroupMuted { Project = id, Group = band, Muted = true },
            new SetGroupSoloed { Project = id, Group = band, Soloed = false },
            new SetGroupVolume { Project = id, Group = band, Volume = 2 },
            new MoveGroup { Project = id, Group = band, Direction = MoveDirection.Up },
            new AddTrack { Project = id },
            new AddTrack { Project = id, Kind = TrackKind.Bus },
            new AddTrack { Project = id, Kind = TrackKind.Instrument, Preset = "Organ" },
            new RemoveTrack { Project = id, Track = vocals.Id },
            new RemoveTrack { Project = id, Track = bus.Id },
            new RemoveTrack { Project = id, Track = guitar.Id },
            new RenameTrack { Project = id, Track = keys.Id, Name = "Piano" },
            new SetTrackColor { Project = id, Track = keys.Id, Color = "#abcdef" },
            new MoveTrack { Project = id, Track = keys.Id, Direction = MoveDirection.Down },
            new MoveTrack { Project = id, Track = vocals.Id, Direction = MoveDirection.Down },
            new SetTrackVolume { Project = id, Track = vocals.Id, Volume = 1.2 },
            new SetTrackPan { Project = id, Track = vocals.Id, Pan = 0.5 },
            new SetTrackMuted { Project = id, Track = keys.Id, Muted = true },
            new SetTrackSoloed { Project = id, Track = keys.Id, Soloed = true },
            new SetMasterVolume { Project = id, Volume = 0.5 },
            new SetMasterLimiter { Project = id, Enabled = false },
            new AddSend { Project = id, Track = guitar.Id, Bus = bus.Id },
            new RemoveSend { Project = id, Track = vocals.Id, Bus = vocals.Sends[0].Bus },
            new SetSendLevel { Project = id, Track = vocals.Id, Bus = vocals.Sends[0].Bus, Level = 0.1 },
            new SetSendPre { Project = id, Track = vocals.Id, Bus = vocals.Sends[0].Bus, Pre = !vocals.Sends[0].Pre },
            new SetSendMuted { Project = id, Track = vocals.Id, Bus = vocals.Sends[0].Bus, Muted = !vocals.Sends[0].Muted },
        ];

        foreach (Command command in commands)
        {
            CommandRig rig = new(sample);
            CommandResult result = rig.Run(command);
            Assert.True(result.Succeeded, $"{command.GetType().Name}: {string.Join("; ", result.Errors.Select(error => error.Message))}");
            Assert.Empty(ProjectValidator.Validate(result.Document!));
            Assert.True(sample != result.Document, $"{command.GetType().Name} changed nothing");
        }
    }

    [Fact]
    public void EveryMidiCommandRunsAgainstTheSampleProject()
    {
        ProjectDocument sample = Sample();
        Track keys = sample.Tracks.First(track => track.Kind == TrackKind.Instrument && track.MidiClips.Count > 0);
        MidiClip clip = keys.MidiClips[0];
        NoteId first = clip.Notes[0].Id;
        ProjectId id = sample.Id;
        Command[] commands =
        [
            new AddNote { Project = id, Clip = clip.Id, Pitch = 67, Start = 3 },
            new MoveNotes { Project = id, Clip = clip.Id, Notes = [new NoteTarget(first, 0.25, 61)] },
            new MoveNotes { Project = id, Clip = clip.Id, Notes = [new NoteTarget(clip.Notes[1].Id, 0.9, 65)] },
            new ResizeNotes { Project = id, Clip = clip.Id, Notes = [new NoteLength(first, 0.3)] },
            new SetNoteVelocity { Project = id, Clip = clip.Id, Notes = [first], Velocity = 20 },
            new TransposeNotes { Project = id, Clip = clip.Id, Semitones = 7 },
            new QuantizeNotes { Project = id, Clip = clip.Id },
            new DeleteNotes { Project = id, Clip = clip.Id, Notes = [first] },
            new AddControlChange { Project = id, Clip = clip.Id, Time = 3, Controller = 1, Value = 40 },
            new RemoveControlChanges { Project = id, Clip = clip.Id, Time = 2, Controller = 64 },
            new ClearControlChanges { Project = id, Clip = clip.Id },
            new ClearControlChanges { Project = id, Clip = clip.Id, Controller = 64 },
        ];

        foreach (Command command in commands)
        {
            CommandRig rig = new(sample);
            CommandResult result = rig.Run(command);
            Assert.True(result.Succeeded, $"{command.GetType().Name}: {string.Join("; ", result.Errors.Select(error => error.Message))}");
            Assert.Empty(ProjectValidator.Validate(result.Document!));
            Assert.True(sample != result.Document, $"{command.GetType().Name} changed nothing");
        }
    }

    [Fact]
    public void EveryAutomationCommandRunsAgainstTheSampleProject()
    {
        ProjectDocument sample = Sample();
        Track track = sample.Tracks.First(candidate => candidate.Automation.Volume.Count >= 2 && candidate.Automation.Pan.Count >= 2);
        ProjectId id = sample.Id;
        Command[] commands =
        [
            new AddAutomationPoint { Project = id, Track = track.Id, Parameter = AutomationParameter.Volume, Time = 2, Value = 1.2 },
            new AddAutomationPoint { Project = id, Track = track.Id, Parameter = AutomationParameter.Pan, Time = 3, Value = 0.25 },
            new MoveAutomationPoint { Project = id, Track = track.Id, Parameter = AutomationParameter.Volume, Index = 0, Time = 0.5, Value = 0.3 },
            new DeleteAutomationPoint { Project = id, Track = track.Id, Parameter = AutomationParameter.Pan, Index = 1 },
            new SetAutomationValues
            {
                Project = id, Track = track.Id, Parameter = AutomationParameter.Pan, Points = [new PointValue(0, 0.5), new PointValue(1, -0.5)],
            },
            new CommitAutomationWrite
            {
                Project = id, Track = track.Id, Parameter = AutomationParameter.Volume, ReturnToOld = true,
                Points = [new WrittenPoint(1, 1.5), new WrittenPoint(2, 1.6)],
            },
            new SetAutomationMode { Project = id, Track = track.Id, Mode = AutomationMode.Off },
            new SetAutomationView { Project = id, Track = track.Id, Visible = !track.View.AutomationVisible },
            new SetAutomationView { Project = id, Track = track.Id, Parameter = AutomationParameter.Volume },
        ];

        foreach (Command command in commands)
        {
            CommandRig rig = new(sample);
            CommandResult result = rig.Run(command);
            Assert.True(result.Succeeded, $"{command.GetType().Name}: {string.Join("; ", result.Errors.Select(error => error.Message))}");
            Assert.Empty(ProjectValidator.Validate(result.Document!));
            Assert.True(sample != result.Document, $"{command.GetType().Name} changed nothing");
        }
    }

    [Fact]
    public void EveryBeatCommandRunsAgainstTheSampleProject()
    {
        ProjectDocument sample = Sample();
        BeatLane kick = sample.BeatPattern.Lanes.First(lane => lane.Media is not null);
        ProjectId id = sample.Id;
        Command[] commands =
        [
            new AddBeatLane { Project = id, Name = "Clap", Media = MediaId.From("med_clap") },
            new RemoveBeatLane { Project = id, Lane = kick.Id },
            new RenameBeatLane { Project = id, Lane = kick.Id, Name = "Thump" },
            new SetBeatLaneGain { Project = id, Lane = kick.Id, Gain = 0.4 },
            new SetBeatLaneMedia { Project = id, Lane = kick.Id },
            new SetBeatLaneSteps { Project = id, Lane = kick.Id, Steps = [1, 2], On = true },
            new SetBeatLaneSteps { Project = id, Lane = kick.Id, Steps = [0], On = false },
            new SetBeatSteps { Project = id, Steps = 2 },
            new SetBeatSteps { Project = id, Steps = 64 },
            new SetBeatSwing { Project = id, Swing = 0.4 },
        ];

        foreach (Command command in commands)
        {
            CommandRig rig = new(sample);
            CommandResult result = rig.Run(command);
            Assert.True(result.Succeeded, $"{command.GetType().Name}: {string.Join("; ", result.Errors.Select(error => error.Message))}");
            Assert.Empty(ProjectValidator.Validate(result.Document!));
            Assert.True(sample != result.Document, $"{command.GetType().Name} changed nothing");
        }
    }

    [Fact]
    public void EveryLyricsScoreAndStemCommandRunsAgainstTheSampleProject()
    {
        ProjectDocument sample = Sample();
        Track track = sample.Tracks.First(candidate => candidate.Clips.Count >= 2 && candidate.Clips[0].Meta?.Lyrics is not null);
        ClipId clip = track.Clips[0].Id;
        ClipId stem = track.Clips[1].Id;
        ProjectId id = sample.Id;
        Command[] commands =
        [
            new SetLyricsText { Project = id, Clip = clip, Text = "la la la" },
            new SetLyricsText { Project = id, Clip = clip, Text = string.Empty },
            new ApplyLyricsAlignment
            {
                Project = id, Clip = clip, Text = "hello world", Words = [new HeardWord("hello", 0, 1), new HeardWord("world", 1, 2)],
            },
            new ApplyLyricsTranscript { Project = id, Clip = clip, Words = [new HeardWord("hello", 0, 1)] },
            new EstimateLyricsTiming { Project = id, Clip = clip, Text = "one two" },
            new ClearLyricsTiming { Project = id, Clip = clip },
            new ShiftLyrics { Project = id, Clip = clip, Seconds = 1 },
            new SetLyricsWordTime { Project = id, Clip = clip, Word = 0, Start = 0.55 },
            new ImportLyricsLrc { Project = id, Clip = clip, Lrc = "[00:03.00]la la" },
            new SetClipScore { Project = id, Clip = clip, Abc = "X:2" },
            new SetClipScore { Project = id, Clip = stem, Label = "Stem score" },
            new SetScoreLabel { Project = id, Clip = clip, Label = "Version B" },
            new ClearClipScore { Project = id, Clip = clip },
            new SetClipStem { Project = id, Clip = stem, Source = clip, Stem = "drums" },
            new ClearClipStem { Project = id, Clip = stem },
        ];

        foreach (Command command in commands)
        {
            CommandRig rig = new(sample);
            CommandResult result = rig.Run(command);
            Assert.True(result.Succeeded, $"{command.GetType().Name}: {string.Join("; ", result.Errors.Select(error => error.Message))}");
            Assert.Empty(ProjectValidator.Validate(result.Document!));
            Assert.True(sample != result.Document, $"{command.GetType().Name} changed nothing");
        }
    }

    [Fact]
    public void EditsOfTheLyricsKeepTheirUnknownFieldsAndANewAnswerOfAModelStartsAFreshRecord()
    {
        ProjectDocument sample = Sample();
        Track track = sample.Tracks.First(candidate => candidate.Clips.Count >= 2 && candidate.Clips[0].Meta?.Lyrics is not null);
        ClipId clip = track.Clips[0].Id;
        ExtraFields old = track.Clips[0].Meta!.Lyrics!.Extra;
        ProjectId id = sample.Id;
        Command[] edits =
        [
            new SetLyricsText { Project = id, Clip = clip, Text = "[Verse]\nla la" },
            new ShiftLyrics { Project = id, Clip = clip, Seconds = 1 },
            new SetLyricsWordTime { Project = id, Clip = clip, Word = 0, Start = 0.55 },
        ];
        Command[] fresh =
        [
            new ClearLyricsTiming { Project = id, Clip = clip },
            new SetLyricsText { Project = id, Clip = clip, Text = "other words entirely" },
            new ApplyLyricsAlignment { Project = id, Clip = clip, Text = "la", Words = [new HeardWord("la", 0, 1)] },
            new ApplyLyricsTranscript { Project = id, Clip = clip, Words = [new HeardWord("la", 0, 1)] },
            new EstimateLyricsTiming { Project = id, Clip = clip, Text = "la la" },
            new ImportLyricsLrc { Project = id, Clip = clip, Lrc = "[00:03.00]la la" },
        ];

        Assert.False(old.IsEmpty);
        foreach (Command command in edits)
        {
            CommandResult result = new CommandRig(sample).Run(command);
            Assert.True(result.Succeeded, command.GetType().Name);
            Assert.Equal(old, result.Document!.Tracks.First(candidate => candidate.Id == track.Id).Clips[0].Meta!.Lyrics!.Extra);
        }
        foreach (Command command in fresh)
        {
            CommandResult result = new CommandRig(sample).Run(command);
            Assert.True(result.Succeeded, command.GetType().Name);
            ClipLyrics lyrics = result.Document!.Tracks.First(candidate => candidate.Id == track.Id).Clips[0].Meta!.Lyrics!;
            Assert.True(lyrics.Extra.IsEmpty, command.GetType().Name);
        }
    }

    [Fact]
    public void EveryProjectTrackAndInstrumentSettingsCommandRunsAgainstTheSampleProject()
    {
        ProjectDocument sample = Sample();
        Track keys = sample.Tracks.First(track => track.Kind == TrackKind.Instrument && track.Instrument is SynthInstrument);
        Track sampler = sample.Tracks.First(track => track.Instrument is SamplerInstrument);
        Track audio = sample.Tracks.First(track => track.Kind == TrackKind.Audio);
        SynthInstrument keySynth = (SynthInstrument)keys.Instrument!;
        SamplerInstrument keySampler = (SamplerInstrument)sampler.Instrument!;
        ProjectId id = sample.Id;
        Command[] commands =
        [
            new RenameProject { Project = id, Name = "Renamed" },
            new SetMetronome { Project = id, Enabled = !sample.Metronome },
            new SetProjectView { Project = id, Ruler = sample.View.Ruler == RulerMode.Time ? RulerMode.Beats : RulerMode.Time },
            new SetProjectView { Project = id, EditMode = sample.View.EditMode == EditMode.Grid ? EditMode.Slip : EditMode.Grid },
            new SetProjectView { Project = id, Grid = sample.View.Grid == "1/8" ? "bar" : "1/8" },
            new SetTrackArmed { Project = id, Track = audio.Id, Armed = !audio.Armed },
            new SetTrackMonitor { Project = id, Track = audio.Id, Monitor = !audio.Monitor },
            new SetTrackInput { Project = id, Track = audio.Id, DeviceId = "other-device", Channels = InputChannelMode.Single, Channel = 4 },
            new SetTrackHeight { Project = id, Track = audio.Id, Height = audio.View.Height + 10 },
            new SetTrackIcon { Project = id, Track = audio.Id, Icon = "\U0001F3A4" },
            new SetInstrumentPreset { Project = id, Track = keys.Id, Preset = "Bell" },
            new SetInstrumentPreset { Project = id, Track = keys.Id, Preset = InstrumentPresets.Sampler },
            new SetInstrumentParameter { Project = id, Track = keys.Id, Parameter = "resonance", Value = keySynth.Resonance + 1 },
            new SetOscillatorWave { Project = id, Track = keys.Id, Oscillator = 1, Wave = OscillatorWave.Square },
            new SetSamplerRootNote { Project = id, Track = sampler.Id, Note = keySampler.RootNote + 1 },
            new SetSamplerOneShot { Project = id, Track = sampler.Id, OneShot = !keySampler.OneShot },
            new SetSamplerSample { Project = id, Track = sampler.Id, Sample = MediaId.From("med_other"), SampleName = "Other" },
        ];

        foreach (Command command in commands)
        {
            CommandRig rig = new(sample);
            CommandResult result = rig.Run(command);
            Assert.True(result.Succeeded, $"{command.GetType().Name}: {string.Join("; ", result.Errors.Select(error => error.Message))}");
            Assert.Empty(ProjectValidator.Validate(result.Document!));
            Assert.True(sample != result.Document, $"{command.GetType().Name} changed nothing");
        }
    }

    [Fact]
    public void EveryEffectCommandRunsAgainstTheSampleProject()
    {
        ProjectDocument sample = Sample();
        Track track = sample.Tracks.First(candidate => candidate.Effects.Count >= 2);
        Effect compressor = track.Effects.First(effect => effect is CompressorEffect);
        Effect delay = track.Effects.First(effect => effect is DelayEffect);
        ProjectId id = sample.Id;
        List<Command> commands =
        [
            new RemoveEffect { Project = id, Track = track.Id, Effect = compressor.Id },
            new MoveEffect { Project = id, Track = track.Id, Effect = track.Effects[0].Id, Direction = MoveDirection.Down },
            new SetEffectEnabled { Project = id, Track = track.Id, Effect = compressor.Id, Enabled = !compressor.Enabled },
            new SetEffectParameter { Project = id, Track = track.Id, Effect = compressor.Id, Parameter = "makeup", Value = EffectRules.Find(compressor, "makeup")!.Get(compressor) + 1 },
            new SetDelaySync { Project = id, Track = track.Id, Effect = delay.Id, Sync = !((DelayEffect)delay).Sync },
            new ApplyEffectPreset { Project = id, Track = track.Id, Effect = compressor.Id, Preset = "Drum smash" },
        ];
        commands.AddRange(Enum.GetValues<EffectKind>().Select(kind => (Command)new AddEffect { Project = id, Track = sample.Tracks[0].Id, Kind = kind }));

        foreach (Command command in commands)
        {
            CommandRig rig = new(sample);
            CommandResult result = rig.Run(command);
            Assert.True(result.Succeeded, $"{command.GetType().Name}: {string.Join("; ", result.Errors.Select(error => error.Message))}");
            Assert.Empty(ProjectValidator.Validate(result.Document!));
            Assert.True(sample != result.Document, $"{command.GetType().Name} changed nothing");
        }
    }

    [Fact]
    public void EveryClipCommandRunsAgainstTheSampleProject()
    {
        ProjectDocument sample = Sample();
        Track audio = sample.Tracks.First(track => track.Clips.Count > 0);
        Track other = sample.Tracks.First(track => track.Kind == TrackKind.Audio && track.Id != audio.Id);
        Track keys = sample.Tracks.First(track => track.Kind == TrackKind.Instrument && track.MidiClips.Count > 0);
        AudioClip clip = audio.Clips[0];
        MidiClip midi = keys.MidiClips[0];
        ProjectId id = sample.Id;
        double middle = clip.Start + (ClipRules.VisibleLength(clip) / 2);
        Command[] commands =
        [
            new AddClip { Project = id, Track = other.Id, Media = clip.Media, Duration = 5 },
            new AddMidiClip { Project = id, Track = keys.Id, Start = 100, Length = 2 },
            new MoveClip { Project = id, Clip = clip.Id, Start = clip.Start + 1 },
            new MoveClip { Project = id, Clip = clip.Id, Start = clip.Start, Track = other.Id },
            new MoveClip { Project = id, Clip = midi.Id, Start = midi.Start + 1 },
            new TrimClip { Project = id, Clip = clip.Id, Edge = ClipEdge.Start, Time = clip.Start + 1 },
            new TrimClip { Project = id, Clip = clip.Id, Edge = ClipEdge.End, Time = ClipRules.EndOf(clip) - 1 },
            new SplitClip { Project = id, Clip = clip.Id, At = middle },
            new DuplicateClip { Project = id, Clip = clip.Id },
            new DuplicateClip { Project = id, Clip = midi.Id },
            new DeleteClip { Project = id, Clip = clip.Id },
            new DeleteClip { Project = id, Clip = midi.Id },
            new SetClipGain { Project = id, Clip = clip.Id, Gain = clip.Gain + 0.5 },
            new SetClipFades { Project = id, Clip = clip.Id, FadeIn = clip.FadeIn + 0.01 },
            new SetMidiClipLength { Project = id, Clip = midi.Id, Length = midi.Length + 1 },
            new SetClipMuted { Project = id, Clip = clip.Id, Muted = !clip.Muted },
            new SetClipMuted { Project = id, Clip = midi.Id, Muted = !midi.Muted },
            new RenameClip { Project = id, Clip = clip.Id, Name = "Renamed" },
            new SetClipColor { Project = id, Clip = clip.Id, Color = "#123456" },
            new ReorderClip { Project = id, Clip = audio.Clips[0].Id, Layer = ClipLayer.Front },
        ];

        foreach (Command command in commands)
        {
            CommandRig rig = new(sample);
            CommandResult result = rig.Run(command);
            Assert.True(result.Succeeded, $"{command.GetType().Name}: {string.Join("; ", result.Errors.Select(error => error.Message))}");
            Assert.Empty(ProjectValidator.Validate(result.Document!));
            Assert.True(sample != result.Document, $"{command.GetType().Name} changed nothing");
        }
    }

    [Fact]
    public void RemovingATrackOfTheSampleProjectKeepsItsGroupsAndSendsConsistent()
    {
        CommandRig rig = new(Sample());
        Track guitar = rig.Project.Tracks.First(track => track.Group is not null);
        Track bus = rig.Project.Tracks.First(track => track.Kind == TrackKind.Bus);

        rig.Ok(new RemoveTrack { Project = rig.Id, Track = guitar.Id });
        Assert.Empty(rig.Project.Groups);
        rig.Ok(new RemoveTrack { Project = rig.Id, Track = bus.Id });

        Assert.All(rig.Project.Tracks, track => Assert.DoesNotContain(track.Sends, send => send.Bus == bus.Id));
    }

    [Fact]
    public void RemovingATrackClearsTheLinksOtherClipsHadToItsClips()
    {
        ClipMeta linked = new()
        {
            Score = new ScoreMeta { Abc = "X:1", Parent = ClipId.From("clp_y"), Label = "v2" },
            StemOf = new StemOf(ClipId.From("clp_y"), "vocals"),
        };
        Track x = BlankProject.Track("trk_x") with { Clips = [Clip("clp_x", linked)] };
        Track y = BlankProject.Track("trk_y") with { Clips = [Clip("clp_y")] };
        CommandRig rig = new(BlankProject.Create() with { Tracks = [x, y] });
        ProjectDocument before = rig.Project;

        rig.Ok(new RemoveTrack { Project = rig.Id, Track = y.Id });

        ClipMeta cleared = rig.Project.Tracks[0].Clips[0].Meta!;
        Assert.Null(cleared.Score!.Parent);
        Assert.Equal("v2", cleared.Score.Label);
        Assert.Null(cleared.StemOf!.Value.ClipId);
        Assert.Equal("vocals", cleared.StemOf.Value.Stem);
        rig.History.Undo();
        Assert.Equal(before, rig.Project);
    }

    [Fact]
    public void LinksToClipsThatRemainAreKept()
    {
        ClipMeta linked = new() { Score = new ScoreMeta { Parent = ClipId.From("clp_y") } };
        Track x = BlankProject.Track("trk_x") with { Clips = [Clip("clp_x", linked)] };
        Track y = BlankProject.Track("trk_y") with { Clips = [Clip("clp_y")] };
        Track z = BlankProject.Track("trk_z");

        IReadOnlyList<Track> result = ClipLinks.ClearDangling([x, y, z]);

        Assert.Same(x, result[0]);
        Assert.Same(z, result[2]);
        Assert.Equal(ClipId.From("clp_y"), result[0].Clips[0].Meta!.Score!.Parent);
    }

    [Fact]
    public void AddingAClipToATrackWithTheMostClipsIsOutOfRange()
    {
        AudioClip template = Clip("clp_0");
        AudioClip[] full = [.. Enumerable.Range(0, ProjectLimits.MaxClips).Select(index => template with { Id = ClipId.From($"clp_{index}") })];
        Track track = BlankProject.Track("trk_1") with { Clips = [.. full] };
        CommandRig rig = new(BlankProject.Create() with { Tracks = [track] });

        CommandResult add = rig.Refused(new AddClip { Project = rig.Id, Track = track.Id, Media = template.Media, Duration = 1 });
        CommandResult copy = rig.Refused(new DuplicateClip { Project = rig.Id, Clip = full[0].Id });
        CommandResult split = rig.Refused(new SplitClip { Project = rig.Id, Clip = full[0].Id, At = 5 });

        Assert.All(new[] { add, copy, split }, result => Assert.Equal(CommandErrorCode.Invalid, result.Errors[0].Code));
        Assert.All(new[] { add, copy, split }, result => Assert.Contains("at most", result.Errors[0].Message, StringComparison.Ordinal));
    }

    [Fact]
    public void AddingAGroupToAProjectWithTheMostGroupsIsOutOfRange()
    {
        TrackGroup template = new() { Id = GroupId.From("grp_0"), Name = "G", Color = "#7a8cff", Collapsed = false, Tint = true, Muted = false, Soloed = false, Volume = 1 };
        TrackGroup[] full = [.. Enumerable.Range(0, ProjectLimits.MaxSends).Select(index => template with { Id = GroupId.From($"grp_{index}") })];
        Track member = BlankProject.Track("trk_1") with { Group = full[0].Id };
        CommandRig rig = new(BlankProject.Create() with { Tracks = [member], Groups = [.. full] });

        CommandResult result = rig.Refused(new CreateGroup { Project = rig.Id, Tracks = [member.Id] });

        Assert.Equal(CommandErrorCode.Invalid, result.Errors[0].Code);
        Assert.Contains("at most", result.Errors[0].Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddingATrackToAFullProjectIsOutOfRange()
    {
        Track[] full = [.. Enumerable.Range(0, ProjectLimits.MaxTracks).Select(index => BlankProject.Track($"trk_{index}"))];
        CommandRig rig = new(BlankProject.Create() with { Tracks = [.. full] });

        CommandResult result = rig.Refused(new AddTrack { Project = rig.Id });

        Assert.Equal(CommandErrorCode.Invalid, result.Errors[0].Code);
        Assert.Contains("at most", result.Errors[0].Message, StringComparison.Ordinal);
        Assert.Empty(result.Created);
    }
}
