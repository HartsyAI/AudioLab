using AudioLab.Core.Model;

namespace AudioLab.Audio.Tests.Support;

/// <summary>Builds small valid projects for the render tests.</summary>
public static class TestProject
{
    public static ProjectDocument Create(params Track[] tracks) => new()
    {
        Version = ProjectDocument.CurrentVersion,
        Id = ProjectId.From("render-test"),
        Owner = UserId.Local,
        Name = "Render test",
        Bpm = 120,
        TempoMap = [],
        TimeSignature = new TimeSignature(4, 4),
        MasterVolume = 1,
        MasterLimiter = false,
        Metronome = false,
        Markers = [],
        Sections = [],
        Groups = [],
        BeatPattern = new BeatPattern { Steps = 16, Swing = 0, Lanes = [] },
        Tracks = new ValueList<Track>(tracks),
        View = new ProjectView { Ruler = RulerMode.Beats, EditMode = EditMode.Slip, Grid = "1/4" },
    };

    public static Track Track(string id, TrackKind kind = TrackKind.Audio, params AudioClip[] clips) => new()
    {
        Id = TrackId.From(id),
        Name = id,
        Kind = kind,
        Color = "#4a9eff",
        Volume = 1,
        Pan = 0,
        Muted = false,
        Soloed = false,
        Armed = false,
        Monitor = false,
        Input = new TrackInput(null, InputChannelMode.Mono),
        TakeCount = 0,
        Clips = new ValueList<AudioClip>(clips),
        MidiClips = [],
        Effects = [],
        Automation = new TrackAutomation { Mode = AutomationMode.Read, Volume = [], Pan = [] },
        Sends = [],
        View = new TrackView { Height = 100, AutomationVisible = false, AutomationParameter = AutomationParameter.Volume },
    };

    /// <summary>A clip that plays <paramref name="seconds"/> of a media file from <paramref name="start"/> on the timeline.</summary>
    public static AudioClip Clip(string id, string media, double start, double seconds, double offset = 0, double gain = 1) => new()
    {
        Id = ClipId.From(id),
        Name = id,
        Media = MediaId.From(media),
        Start = start,
        Duration = seconds + offset,
        Offset = offset,
        TrimEnd = 0,
        Gain = gain,
        FadeIn = 0,
        FadeOut = 0,
        Muted = false,
    };
}
