using System.Text.Json;
using AudioLab.Core.Commands;
using AudioLab.Core.Model;
using AudioLab.Core.Realtime;
using AudioLab.Server.Mcp;
using AudioLab.Server.Store;
using ModelContextProtocol.Protocol;

namespace AudioLab.Server.Tests;

public sealed class McpProjectReaderTests
{
    private static readonly TrackId _drums = TrackId.From("trk_drums");
    private static readonly TrackId _keys = TrackId.From("trk_keys");
    private static readonly ClipId _riff = ClipId.From("clp_riff");

    [Fact]
    public void TheSummaryCountsWhatTheProjectHoldsAndListsTheTracksWithTheirIds()
    {
        JsonElement summary = Read(new GetProjectArguments { Project = Document().Id });

        Assert.Equal(7, summary.GetProperty("revision").GetInt64());
        Assert.Equal("Song", summary.GetProperty("name").GetString());
        Assert.Equal(2, summary.GetProperty("trackCount").GetInt32());
        JsonElement[] tracks = [.. summary.GetProperty("tracks").EnumerateArray()];
        Assert.Equal(new[] { "trk_drums", "trk_keys" }, tracks.Select(track => track.GetProperty("id").GetString()).ToArray());
        Assert.Equal((2, 0), (tracks[0].GetProperty("clipCount").GetInt32(), tracks[0].GetProperty("midiClipCount").GetInt32()));
        Assert.Equal((0, 1), (tracks[1].GetProperty("clipCount").GetInt32(), tracks[1].GetProperty("midiClipCount").GetInt32()));
        Assert.False(summary.TryGetProperty("markers", out _));
    }

    [Fact]
    public void TheSettingsAreEverythingButTheBulkySections()
    {
        JsonElement settings = Read(new GetProjectArguments { Project = Document().Id, Section = ProjectSection.Settings });

        Assert.Equal(120, settings.GetProperty("bpm").GetDouble());
        Assert.True(settings.TryGetProperty("view", out _));
        foreach (string bulky in new[] { "markers", "sections", "groups", "beatPattern", "tracks" })
        {
            Assert.False(settings.TryGetProperty(bulky, out _), bulky);
        }
    }

    [Fact]
    public void TracksComeWithoutTheirClipsAndOneCanBeAskedFor()
    {
        JsonElement all = Read(new GetProjectArguments { Project = Document().Id, Section = ProjectSection.Tracks });
        Assert.Equal(2, all.GetProperty("total").GetInt32());
        JsonElement drums = all.GetProperty("items")[0];
        Assert.Equal(2, drums.GetProperty("clipCount").GetInt32());
        Assert.False(drums.TryGetProperty("clips", out _));
        Assert.False(drums.TryGetProperty("midiClips", out _));

        JsonElement one = Read(new GetProjectArguments { Project = Document().Id, Section = ProjectSection.Tracks, Track = _keys });
        Assert.Equal("trk_keys", Assert.Single(one.GetProperty("items").EnumerateArray()).GetProperty("id").GetString());
    }

    [Fact]
    public void ClipsAndNotesAreReadFromOneTrackAndClipAndAPageSaysItsTotal()
    {
        JsonElement audio = Read(new GetProjectArguments { Project = Document().Id, Section = ProjectSection.AudioClips, Track = _drums });
        Assert.Equal(2, audio.GetProperty("total").GetInt32());
        JsonElement second = Read(new GetProjectArguments
        {
            Project = Document().Id,
            Section = ProjectSection.AudioClips,
            Track = _drums,
            Offset = 1,
            Count = 1,
        });
        Assert.Equal(2, second.GetProperty("total").GetInt32());
        Assert.Equal(1, second.GetProperty("offset").GetInt32());
        Assert.Equal(1, second.GetProperty("items").GetArrayLength());

        JsonElement midi = Read(new GetProjectArguments { Project = Document().Id, Section = ProjectSection.MidiClips, Track = _keys });
        JsonElement clip = Assert.Single(midi.GetProperty("items").EnumerateArray());
        Assert.Equal(3, clip.GetProperty("noteCount").GetInt32());
        Assert.False(clip.TryGetProperty("notes", out _));

        JsonElement notes = Read(new GetProjectArguments { Project = Document().Id, Section = ProjectSection.Notes, Track = _keys, Clip = _riff });
        int[] pitches = [.. notes.GetProperty("items").EnumerateArray().Select(note => note.GetProperty("pitch").GetInt32())];
        Assert.Equal(new[] { 60, 62, 64 }, pitches);
        JsonElement changes = Read(Arguments(ProjectSection.ControlChanges, _keys, _riff));
        Assert.Equal(0, changes.GetProperty("total").GetInt32());
    }

    [Fact]
    public void ASectionThatNeedsATrackOrAClipSaysSoAndAnUnknownOneIsNotFoundAndANegativeRangeIsMalformed()
    {
        (GetProjectArguments Arguments, CommandErrorCode Code)[] cases =
        [
            (Arguments(ProjectSection.AudioClips), CommandErrorCode.Malformed),
            (Arguments(ProjectSection.Notes, _keys), CommandErrorCode.Malformed),
            (Arguments(ProjectSection.Notes, _drums, _riff), CommandErrorCode.NotFound),
            (Arguments(ProjectSection.Tracks, TrackId.From("trk_none")), CommandErrorCode.NotFound),
            (new GetProjectArguments { Project = Document().Id, Section = ProjectSection.Markers, Offset = -1 }, CommandErrorCode.Malformed),
        ];
        foreach ((GetProjectArguments arguments, CommandErrorCode code) in cases)
        {
            CallToolResult result = McpProjectReader.Read(new ProjectSnapshot(Document(), 7), arguments);
            Assert.True(result.IsError);
            CommandReply reply = McpTestClient.Reply(result);
            Assert.Equal((code, 7L), (Assert.Single(reply.Errors).Code, reply.Revision));
        }
    }

    [Fact]
    public void EverySectionOfTheEnumIsReadableSoNoneIsOfferedAndRefused()
    {
        foreach (ProjectSection section in Enum.GetValues<ProjectSection>())
        {
            GetProjectArguments arguments = new() { Project = Document().Id, Section = section, Track = _keys, Clip = _riff };
            Assert.NotEqual(true, McpProjectReader.Read(new ProjectSnapshot(Document(), 7), arguments).IsError);
        }
    }

    private static GetProjectArguments Arguments(ProjectSection section, TrackId? track = null, ClipId? clip = null)
    {
        return new GetProjectArguments { Project = Document().Id, Section = section, Track = track, Clip = clip };
    }

    private static JsonElement Read(GetProjectArguments arguments)
    {
        CallToolResult result = McpProjectReader.Read(new ProjectSnapshot(Document(), 7), arguments);
        Assert.NotEqual(true, result.IsError);
        return McpTestClient.Json(result);
    }

    private static ProjectDocument Document()
    {
        Track drums = ProjectDefaults.NewTrack(_drums, "Drums", TrackKind.Audio, "#abc") with
        {
            Clips = [Audio("clp_a", 0), Audio("clp_b", 4)],
        };
        Track keys = ProjectDefaults.NewTrack(_keys, "Keys", TrackKind.Instrument, "#def", InstrumentPresets.CreateSynth("Soft Keys")) with
        {
            MidiClips =
            [
                new MidiClip
                {
                    Id = _riff,
                    Name = "Riff",
                    Start = 0,
                    Length = 4,
                    NextNoteId = new NoteId(4),
                    Notes =
                    [
                        new MidiNote(new NoteId(1), 60, 0, 1, 100),
                        new MidiNote(new NoteId(2), 62, 1, 1, 100),
                        new MidiNote(new NoteId(3), 64, 2, 1, 100),
                    ],
                    ControlChanges = [],
                    Muted = false,
                },
            ],
        };
        return new ProjectDocument
        {
            Version = ProjectDocument.CurrentVersion,
            Id = ProjectId.From("prj_reader"),
            Owner = UserId.Local,
            Name = "Song",
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
            Tracks = [drums, keys],
            View = new ProjectView { Ruler = RulerMode.Beats, EditMode = EditMode.Slip, Grid = "1/4" },
        };
    }

    private static AudioClip Audio(string id, double start)
    {
        return new AudioClip
        {
            Id = ClipId.From(id),
            Name = id,
            Media = MediaId.From("med_one"),
            Start = start,
            Duration = 4,
            Offset = 0,
            TrimEnd = 0,
            Gain = 1,
            FadeIn = 0,
            FadeOut = 0,
            Muted = false,
        };
    }
}
