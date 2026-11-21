using System.Globalization;

namespace AudioLab.Web.Session;

/// <summary>Builds a session of any size. Tracks 1 to 6 and their first clips are the design mock's, so the top left matches it.</summary>
public static class SyntheticSession
{
    private const double BaseBarWidth = 68;

    private static readonly MockTrack[] _mockTracks =
    [
        new(1, "Lead vocal", "In 1 · Mic", 2, TrackKind.Vocal, "-6.0", 62, 58, 52, 0, ["EQ", "Compressor"], [(5, 4, "Verse 1 · take 3", false), (9, 4, "Chorus · take 2", false), (13, 4, "Verse 2 · take 1", false)]),
        new(2, "Backing vocals", "In 2 · Mic", 4, TrackKind.Vocal, "-11.5", 46, 31, 35, -28, ["EQ", "Reverb send"], [(9, 4, "Chorus stack", false), (17, 2, "Bridge oohs", false)]),
        new(3, "Guitar", "In 3 · DI", 6, TrackKind.Guitar, "-8.2", 56, 47, 50, 34, ["Saturation", "Delay send"], [(1, 8, "Rhythm guitar", true), (9, 8, "Rhythm guitar · chorus", false)]),
        new(4, "Keys", "Analog pad", 8, TrackKind.Midi, "-9.0", 53, 38, 36, -12, ["Analog pad", "EQ"], [(5, 8, "Pad chords", false), (13, 6, "Pad chords · lift", false)]),
        new(5, "Bass", "Generated", 9, TrackKind.Bass, "-7.4", 58, 0, 0, 0, ["Compressor", string.Empty], [(1, 16, "Bassline v2 · ACE-Step", false)]),
        new(6, "Drums", "Stem · kit", 10, TrackKind.Drums, "-4.8", 66, 72, 69, 0, ["EQ", "Compressor"], [(1, 4, "Kit intro", false), (5, 14, "Kit loop", false)]),
    ];

    private static readonly (string Name, TrackKind Kind)[] _extraTracks =
    [
        ("Vox double", TrackKind.Vocal), ("Harmony high", TrackKind.Vocal), ("Harmony low", TrackKind.Vocal),
        ("Guitar 2", TrackKind.Guitar), ("Guitar 3 · clean", TrackKind.Guitar), ("Synth lead", TrackKind.Guitar),
        ("Strings", TrackKind.Vocal), ("Brass", TrackKind.Vocal), ("Percussion", TrackKind.Drums),
        ("Shaker", TrackKind.Drums), ("Tambourine", TrackKind.Drums), ("Kick", TrackKind.Drums),
        ("Snare", TrackKind.Drums), ("Hats", TrackKind.Drums), ("Toms", TrackKind.Drums),
        ("Overheads", TrackKind.Drums), ("Room", TrackKind.Drums), ("Bass DI", TrackKind.Bass),
        ("Sub bass", TrackKind.Bass), ("Pad 2", TrackKind.Guitar), ("Arp", TrackKind.Guitar),
        ("Riser", TrackKind.Guitar), ("Impact", TrackKind.Drums), ("Foley", TrackKind.Vocal),
        ("Dialogue", TrackKind.Vocal), ("Ambience", TrackKind.Guitar),
    ];

    private static readonly string[] _clipNames = ["Take", "Fill", "Loop", "Hook", "Verse", "Chorus", "Lift", "Break", "Tail", "Stack"];

    /// <summary>Creates a session with the given number of tracks and clips over the given number of bars.</summary>
    public static SessionModel Create(int trackCount, int clipCount, int bars)
    {
        List<TrackModel> tracks = [];
        List<ClipModel> clips = [];
        int seed = 1;
        Lcg random = new(20261120);
        for (int t = 0; t < trackCount; t++)
        {
            TrackModel track = t < _mockTracks.Length ? FromMock(_mockTracks[t]) : Extra(t, ref random);
            tracks.Add(track);
        }
        int[] targets = new int[trackCount];
        for (int t = 0; t < trackCount; t++)
        {
            targets[t] = (clipCount / trackCount) + (t < clipCount % trackCount ? 1 : 0);
        }
        double[] cursors = new double[trackCount];
        for (int t = 0; t < _mockTracks.Length && t < trackCount; t++)
        {
            foreach ((int bar, int length, string name, bool selected) in _mockTracks[t].Clips)
            {
                seed++;
                tracks[t].Clips.Add(MakeClip(clips.Count, t, bar - 1, length, name, selected, tracks[t].Kind, seed));
                clips.Add(tracks[t].Clips[^1]);
                cursors[t] = Math.Max(cursors[t], bar - 1 + length + 1);
            }
        }
        for (int t = 0; t < trackCount; t++)
        {
            TrackModel track = tracks[t];
            double cursor = cursors[t];
            int index = 0;
            while (track.Clips.Count < targets[t])
            {
                int length = 1 + (int)(random.Next() * 5);
                cursor += (int)(random.Next() * 3);
                if (cursor + length > bars)
                {
                    length = (int)(bars - cursor);
                }
                if (length < 1)
                {
                    break;
                }
                seed++;
                string name = $"{_clipNames[(t + index) % _clipNames.Length]} · take {1 + (index % 4)}";
                track.Clips.Add(MakeClip(clips.Count, t, (int)cursor, length, name, false, track.Kind, seed));
                clips.Add(track.Clips[^1]);
                cursor += length + 1;
                index++;
            }
        }
        return new SessionModel
        {
            Bars = bars,
            Tracks = tracks,
            Strips = Strips(),
            Sections = Sections(bars),
            LoopStartBar = 8,
            LoopBars = 4,
            PlayheadBar = 9.25,
            Clips = clips,
        };
    }

    private static ClipModel MakeClip(int id, int track, int startBar, int length, string name, bool selected, TrackKind kind, int seed)
    {
        double width = (length * BaseBarWidth) - 2;
        bool midi = kind == TrackKind.Midi;
        return new ClipModel
        {
            Id = id,
            Name = name,
            Track = track,
            StartBar = startBar,
            LengthBars = length,
            Selected = selected,
            Peaks = midi ? [] : WaveformSynth.Peaks(seed, Math.Max(24, (int)Math.Round(width / 2.6)), kind),
            Notes = midi ? WaveformSynth.Notes(seed, length) : [],
        };
    }

    private static TrackModel FromMock(MockTrack mock)
    {
        return new TrackModel
        {
            Number = mock.Number,
            Name = mock.Name,
            Source = mock.Source,
            ColourToken = $"--track-{mock.Colour}",
            Kind = mock.Kind,
            DbText = mock.Db,
            Volume = mock.Volume,
            MeterLeft = mock.MeterLeft,
            MeterRight = mock.MeterRight,
            Muted = mock.Number == 5,
            Armed = mock.Number == 1,
            Monitoring = mock.Number == 1,
            Selected = mock.Number == 3,
        };
    }

    private static TrackModel Extra(int index, ref Lcg random)
    {
        (string name, TrackKind kind) = _extraTracks[(index - _mockTracks.Length) % _extraTracks.Length];
        int volume = 38 + (int)(random.Next() * 30);
        return new TrackModel
        {
            Number = index + 1,
            Name = name,
            Source = $"In {(index % 8) + 1} · Mic",
            ColourToken = $"--track-{((index * 5) + 1) % 12 + 1}",
            Kind = kind,
            DbText = (-3 - ((100 - volume) / 8.0)).ToString("0.0", CultureInfo.InvariantCulture),
            Volume = volume,
            MeterLeft = 20 + (int)(random.Next() * 55),
            MeterRight = 20 + (int)(random.Next() * 55),
        };
    }

    private static StripModel[] Strips()
    {
        StripModel Make(string name, string colour, int width, bool master, MockTrack source, string[] slots)
        {
            return new StripModel
            {
                Name = name,
                ColourToken = colour,
                Width = width,
                Master = master,
                Fader = (int)Math.Round((source.Volume * 0.86) + 4),
                Pan = source.Pan,
                PanText = source.Pan == 0 ? "C" : (source.Pan < 0 ? "L" + Math.Round(-source.Pan * 0.75) : "R" + Math.Round(source.Pan * 0.75)),
                DbText = source.Db,
                MeterLeft = source.MeterLeft,
                MeterRight = source.MeterRight,
                Muted = source.Number == 5,
                Slots = slots,
            };
        }

        List<StripModel> strips = [];
        foreach (MockTrack track in _mockTracks)
        {
            strips.Add(Make(track.Name, $"--track-{track.Colour}", 104, false, track, track.Slots));
        }
        strips.Add(Make("Reverb", "--text-faint", 104, false, new(0, string.Empty, string.Empty, 0, TrackKind.Vocal, "-12.0", 44, 26, 29, 0, [], []), ["Plate reverb", "EQ"]));
        strips.Add(Make("Delay", "--text-faint", 104, false, new(0, string.Empty, string.Empty, 0, TrackKind.Vocal, "-15.5", 38, 14, 17, 0, [], []), ["Tempo delay", string.Empty]));
        strips.Add(Make("Master", "--text", 124, true, new(0, string.Empty, string.Empty, 0, TrackKind.Vocal, "-1.2", 74, 79, 77, 0, [], []), ["Limiter", "Loudness meter"]));
        return [.. strips];
    }

    private static SectionModel[] Sections(int bars)
    {
        List<SectionModel> sections =
        [
            new("Intro", 1, 4, "--text-faint"),
            new("Verse 1", 5, 4, "--track-9"),
            new("Chorus", 9, 4, "--track-12"),
            new("Verse 2", 13, 4, "--track-9"),
            new("Bridge", 17, 4, "--track-4"),
        ];
        (string Name, string Colour)[] cycle = [("Chorus", "--track-12"), ("Verse", "--track-9"), ("Build", "--track-7"), ("Drop", "--track-3"), ("Breakdown", "--track-10")];
        int n = 0;
        for (int bar = 21; bar <= bars; bar += 4)
        {
            (string name, string colour) = cycle[n % cycle.Length];
            sections.Add(new SectionModel(name + " " + ((n / cycle.Length) + 2).ToString(CultureInfo.InvariantCulture), bar, Math.Min(4, bars - bar + 1), colour));
            n++;
        }
        return [.. sections];
    }

    private sealed record MockTrack(
        int Number,
        string Name,
        string Source,
        int Colour,
        TrackKind Kind,
        string Db,
        int Volume,
        int MeterLeft,
        int MeterRight,
        int Pan,
        string[] Slots,
        (int Bar, int Length, string Name, bool Selected)[] Clips);
}
