namespace AudioLab.Core.Model;

/// <summary>The starting values shared by whatever creates sections, groups and tracks.</summary>
public static class ProjectDefaults
{
    private static readonly string[] _sectionColors =
    [
        "#f4a261", "#f2c94c", "#b5d63d", "#5fcf80", "#2ec4b6", "#4cc9f0", "#4895ef", "#9d7bf0", "#d97ae0", "#ef6f91",
    ];

    private static readonly string[] _trackColors =
    [
        "#ee6d86", "#ef7d67", "#ee9a55", "#e3ad4f", "#a9cf5b", "#5fcf95",
        "#4fcdb8", "#4fbfdc", "#6c93f2", "#a487ee", "#cf7de0", "#e07ab5",
    ];

    /// <summary>The colour handed to the section at <paramref name="index"/>; the palette wraps.</summary>
    public static string SectionColor(int index) => _sectionColors[Wrap(index, _sectionColors.Length)];

    /// <summary>The colour of the track at <paramref name="index"/>: the design's twelve track tokens, in order.</summary>
    public static string TrackColor(int index) => _trackColors[Wrap(index, _trackColors.Length)];

    /// <summary>A new empty track with the old DAW's starting values: fader 0.8 (a bus 1), centred, 64 pixels tall (the lane height of the session design), mono input.</summary>
    public static Track NewTrack(TrackId id, string name, TrackKind kind, string color, Instrument? instrument = null)
    {
        return new Track
        {
            Id = id,
            Name = name,
            Kind = kind,
            Color = color,
            Volume = kind == TrackKind.Bus ? 1 : 0.8,
            Pan = 0,
            Muted = false,
            Soloed = false,
            Armed = false,
            Monitor = false,
            Input = new TrackInput(null, InputChannelMode.Mono),
            TakeCount = 0,
            Instrument = instrument,
            Clips = [],
            MidiClips = [],
            Effects = [],
            Automation = new TrackAutomation { Mode = AutomationMode.Read, Volume = [], Pan = [] },
            Sends = [],
            View = new TrackView { Height = 64, AutomationVisible = false, AutomationParameter = AutomationParameter.Volume },
        };
    }

    private static int Wrap(int index, int length) => ((index % length) + length) % length;
}
