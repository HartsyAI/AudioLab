using AudioLab.Core.Model;

namespace AudioLab.Core.Tests;

/// <summary>An empty but valid project at 120 BPM in 4/4, for the domain tests to edit.</summary>
internal static class BlankProject
{
    public static ProjectDocument Create()
    {
        return new ProjectDocument
        {
            Version = ProjectDocument.CurrentVersion,
            Id = ProjectId.From("blank-project"),
            Owner = UserId.Local,
            Name = "Blank",
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
            Tracks = [],
            View = new ProjectView { Ruler = RulerMode.Beats, EditMode = EditMode.Slip, Grid = "1/4" },
        };
    }

    public static Track Track(string id, string name = "Track", TrackKind kind = TrackKind.Audio)
    {
        return new Track
        {
            Id = TrackId.From(id),
            Name = name,
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
            Clips = [],
            MidiClips = [],
            Effects = [],
            Automation = new TrackAutomation { Mode = AutomationMode.Read, Volume = [], Pan = [] },
            Sends = [],
            View = new TrackView { Height = 100, AutomationVisible = false, AutomationParameter = AutomationParameter.Volume },
        };
    }
}
