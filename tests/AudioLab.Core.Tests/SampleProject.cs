using System.Text.Json;
using AudioLab.Core.Model;

namespace AudioLab.Core.Tests;

/// <summary>A synthetic version 4 project that uses every part of the model. It is written from the spec, not saved from the app.</summary>
public static class SampleProject
{
    public static ProjectDocument Create(UserId owner)
    {
        SequentialIdService ids = new();
        TrackId vocals = ids.Next<TrackId>();
        TrackId keys = ids.Next<TrackId>();
        TrackId sampler = ids.Next<TrackId>();
        TrackId reverbBus = ids.Next<TrackId>();
        TrackId guitar = ids.Next<TrackId>();
        GroupId band = ids.Next<GroupId>();
        ClipId vocalClip = ids.Next<ClipId>();
        ClipId stemClip = ids.Next<ClipId>();
        MediaId vocalMedia = ids.Next<MediaId>();
        MediaId stemMedia = ids.Next<MediaId>();
        MediaId sampleMedia = ids.Next<MediaId>();
        MediaId kickMedia = ids.Next<MediaId>();
        return new ProjectDocument
        {
            Version = ProjectDocument.CurrentVersion,
            Id = ids.Next<ProjectId>(),
            Owner = owner,
            Name = "Synthetic sample",
            Bpm = 96.5,
            TempoMap = [new TempoChange(ids.Next<TempoChangeId>(), 12.25, 120)],
            TimeSignature = new TimeSignature(6, 8),
            MasterVolume = 0.9,
            MasterLimiter = true,
            Metronome = true,
            Markers =
            [
                new Marker { Id = ids.Next<MarkerId>(), Time = 0, Name = "Start" },
                new Marker { Id = ids.Next<MarkerId>(), Time = 30.5, Name = "Bridge", Color = "#f4a261" },
            ],
            Sections =
            [
                new Section { Id = ids.Next<SectionId>(), Start = 0, End = 15, Name = "Intro", Color = "#f4a261" },
                new Section { Id = ids.Next<SectionId>(), Start = 15, End = 60, Name = "Verse 1", Color = "#5fcf80" },
            ],
            Groups =
            [
                new TrackGroup
                {
                    Id = band,
                    Name = "Band",
                    Color = "#7a8cff",
                    Collapsed = true,
                    Tint = false,
                    Muted = false,
                    Soloed = true,
                    Volume = 0.75,
                },
            ],
            BeatPattern = new BeatPattern
            {
                Steps = 4,
                Swing = 0.25,
                Lanes =
                [
                    new BeatLane { Id = ids.Next<BeatLaneId>(), Name = "Kick", Media = kickMedia, Gain = 1, Steps = [true, false, false, true] },
                    new BeatLane { Id = ids.Next<BeatLaneId>(), Name = "Empty", Gain = 0.5, Steps = [false, false, false, false] },
                ],
            },
            View = new ProjectView { Ruler = RulerMode.Beats, EditMode = EditMode.Shuffle, Grid = "1/8t" },
            Tracks =
            [
                VocalTrack(ids, vocals, vocalClip, stemClip, vocalMedia, stemMedia, reverbBus),
                KeysTrack(ids, keys, reverbBus),
                SamplerTrack(sampler, sampleMedia),
                Plain(reverbBus, "Reverb bus", TrackKind.Bus),
                Plain(guitar, "Guitar", TrackKind.Audio) with
                {
                    Group = band,
                    Icon = "🎸",
                    Input = new TrackInput("usb-mic", InputChannelMode.Single, 2),
                },
            ],
        };
    }

    private static Track VocalTrack(SequentialIdService ids, TrackId id, ClipId clip, ClipId stem, MediaId media, MediaId stemMedia, TrackId bus)
    {
        using JsonDocument future = JsonDocument.Parse("{\"nested\":[1,2,{\"deep\":true}]}");
        using JsonDocument aligned = JsonDocument.Parse("\"future\"");
        return Plain(id, "Lead vocal", TrackKind.Audio) with
        {
            Armed = true,
            Monitor = true,
            TakeCount = 3,
            Clips =
            [
                new AudioClip
                {
                    Id = clip,
                    Name = "Take 1",
                    Media = media,
                    Start = 1.5,
                    Duration = 20,
                    Offset = 0.5,
                    TrimEnd = 2,
                    Gain = 0.8,
                    FadeIn = 0.1,
                    FadeOut = 0.2,
                    Muted = false,
                    Color = "#ee6d86",
                    Meta = new ClipMeta
                    {
                        Score = new ScoreMeta
                        {
                            Abc = "X:1\nK:C\nCDEF|",
                            Truncated = false,
                            BudgetSeconds = 30.5,
                            Seed = 4294967295,
                            Label = "Version A",
                            Parent = stem,
                            Created = 1760000000000,
                            Extra = new Dictionary<string, JsonElement> { ["futureField"] = future.RootElement.Clone() },
                        },
                        Lyrics = new ClipLyrics
                        {
                            Text = "[Verse]\nla la la",
                            Source = "transcribed",
                            Extra = new Dictionary<string, JsonElement> { ["alignedBy"] = aligned.RootElement.Clone() },
                            Words = [new LyricWord("la", 0.5, 0.9, 0), new LyricWord("la", 1, 1.01, 0, true)],
                        },
                    },
                },
                new AudioClip
                {
                    Id = stem,
                    Name = "Vocals stem",
                    Media = stemMedia,
                    Start = 0,
                    Duration = 20,
                    Offset = 0,
                    TrimEnd = 0,
                    Gain = 1,
                    FadeIn = 0,
                    FadeOut = 0,
                    Muted = true,
                    Meta = new ClipMeta { StemOf = new StemOf(clip, "vocals") },
                },
            ],
            Effects =
            [
                new EqEffect
                {
                    Id = ids.Next<EffectId>(),
                    Enabled = true,
                    HpFreq = 90,
                    LowGain = -2,
                    LowFreq = 200,
                    MidGain = 2.5,
                    MidFreq = 3000,
                    MidQ = 1.2,
                    HighGain = 2,
                    HighFreq = 8000,
                    LpFreq = 20000,
                },
                new CompressorEffect
                {
                    Id = ids.Next<EffectId>(),
                    Enabled = false,
                    Threshold = -20,
                    Knee = 30,
                    Ratio = 3,
                    Attack = 0.01,
                    Release = 0.2,
                    Makeup = 4,
                },
                new ReverbEffect { Id = ids.Next<EffectId>(), Enabled = true, Size = 2, Decay = 3, Predelay = 0.02, Mix = 0.3 },
                new DelayEffect
                {
                    Id = ids.Next<EffectId>(),
                    Enabled = true,
                    Time = 0.35,
                    Sync = true,
                    Division = "1/8.",
                    Feedback = 0.4,
                    Tone = 8000,
                    Mix = 0.25,
                },
                new SaturationEffect { Id = ids.Next<EffectId>(), Enabled = true, Drive = 0.3, Trim = -1, Mix = 1 },
            ],
            Automation = new TrackAutomation
            {
                Mode = AutomationMode.Latch,
                Volume = [new AutomationPoint(0, 0.8), new AutomationPoint(4, 0.2)],
                Pan = [new AutomationPoint(1, -1), new AutomationPoint(2, 1)],
            },
            Sends = [new Send(bus, 0.5, true, false)],
            View = new TrackView { Height = 64, AutomationVisible = true, AutomationParameter = AutomationParameter.Pan },
        };
    }

    private static Track KeysTrack(SequentialIdService ids, TrackId id, TrackId bus)
    {
        return Plain(id, "Keys", TrackKind.Instrument) with
        {
            Instrument = new SynthInstrument
            {
                Preset = "Soft Keys",
                Osc1 = OscillatorWave.Triangle,
                Osc2 = OscillatorWave.Sine,
                Ratio2 = 2,
                Mix2 = 0.25,
                Detune = 0,
                Cutoff = 3200,
                Resonance = 1,
                FilterEnv = 0.6,
                Attack = 0.004,
                Decay = 0.7,
                Sustain = 0.25,
                Release = 0.45,
                Gain = 0.55,
            },
            MidiClips =
            [
                new MidiClip
                {
                    Id = ids.Next<ClipId>(),
                    Name = "Keys MIDI",
                    Start = 2,
                    Length = 4,
                    Notes = [new MidiNote(NoteId.First, 60, 0, 0.5, 100), new MidiNote(NoteId.First.Next(), 64, 0.5, 1.5, 90)],
                    ControlChanges = [new MidiControlChange(0, 64, 127), new MidiControlChange(2, 64, 0)],
                    Muted = true,
                    Color = "#4fcdb8",
                },
            ],
            Sends = [new Send(bus, 0.25, false, true)],
        };
    }

    private static Track SamplerTrack(TrackId id, MediaId sample)
    {
        return Plain(id, "Sampler", TrackKind.Instrument) with
        {
            Instrument = new SamplerInstrument
            {
                Name = "Sampler",
                Sample = sample,
                SampleName = "snare.wav",
                RootNote = 60,
                Attack = 0.002,
                Release = 0.2,
                Gain = 0.8,
                OneShot = true,
            },
        };
    }

    private static Track Plain(TrackId id, string name, TrackKind kind)
    {
        return new Track
        {
            Id = id,
            Name = name,
            Kind = kind,
            Color = "#4a9eff",
            Volume = 0.8,
            Pan = 0,
            Muted = false,
            Soloed = false,
            Armed = false,
            Monitor = false,
            Input = new TrackInput(null, InputChannelMode.Mono),
            TakeCount = 0,
            Clips = [],
            MidiClips = [],
            Effects = [],
            Automation = new TrackAutomation { Mode = AutomationMode.Read, Volume = [], Pan = [] },
            Sends = [],
            View = new TrackView { Height = 100, AutomationVisible = false, AutomationParameter = AutomationParameter.Volume },
        };
    }
}
