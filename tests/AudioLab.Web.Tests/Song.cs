using AudioLab.Core.Model;

namespace AudioLab.Web.Tests;

/// <summary>Builds small valid documents for the tests of the UI's rules.</summary>
internal static class Song
{
    /// <summary>Empty.</summary>
    public static ProjectDocument Empty(double bpm = 120, IEnumerable<TempoChange>? changes = null)
    {
        return new ProjectDocument
        {
            Version = ProjectDocument.CurrentVersion,
            Id = ProjectId.From("song"),
            Owner = UserId.Local,
            Name = "Song",
            Bpm = bpm,
            TempoMap = [.. changes ?? []],
            TimeSignature = new TimeSignature(4, 4),
            MasterVolume = 1,
            MasterLimiter = false,
            Metronome = false,
            Markers = [],
            Sections = [],
            Groups = [],
            BeatPattern = new BeatPattern { Steps = 16, Swing = 0, Lanes = [] },
            Tracks = [],
            View = new ProjectView { Ruler = RulerMode.Beats, EditMode = EditMode.Grid, Grid = "auto" },
        };
    }

    /// <summary>New track.</summary>
    public static Track NewTrack(string id, double height = 64, params AudioClip[] clips)
    {
        Track track = ProjectDefaults.NewTrack(TrackId.From(id), id, TrackKind.Audio, "#ee6d86");
        return track with { View = track.View with { Height = height }, Clips = [.. clips] };
    }

    /// <summary>With midi.</summary>
    public static Track WithMidi(string id, params MidiClip[] clips)
    {
        Track track = ProjectDefaults.NewTrack(TrackId.From(id), id, TrackKind.Instrument, "#6c93f2", InstrumentPresets.CreateSynth("Soft Keys"));
        return track with { View = track.View with { Height = 64 }, MidiClips = [.. clips] };
    }

    /// <summary>Audio.</summary>
    public static AudioClip Audio(string id, double start, double length)
    {
        return new AudioClip
        {
            Id = ClipId.From(id),
            Name = id,
            Media = MediaId.From("media-" + id),
            Start = start,
            Duration = length,
            Offset = 0,
            TrimEnd = 0,
            Gain = 1,
            FadeIn = 0,
            FadeOut = 0,
            Muted = false,
        };
    }

    /// <summary>Midi.</summary>
    public static MidiClip Midi(string id, double start, double length, params MidiNote[] notes)
    {
        return new MidiClip
        {
            Id = ClipId.From(id),
            Name = id,
            Start = start,
            Length = length,
            NextNoteId = new NoteId(notes.Length + 1),
            Notes = [.. notes],
            ControlChanges = [],
            Muted = false,
        };
    }

    /// <summary>With.</summary>
    public static ProjectDocument With(this ProjectDocument document, params Track[] tracks) => document with { Tracks = [.. tracks] };
}
