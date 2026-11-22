using System.IO.Compression;
using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Audio;

/// <summary>Renders each track, and each aux bus that is fed, on its own, so that the stems add up to the mix.</summary>
/// <remarks>
/// A track stem is the track alone and dry; a bus stem is what the tracks send into it. Solo is ignored, the master volume is at unity
/// and there is no limiter, and a muted track gives a silent stem, as it adds nothing to the mix. Every stem runs to the same length,
/// that of the longest tail among them.
/// </remarks>
public static class StemExporter
{
    private const int MaxFileNameLength = 100;

    /// <summary>The tracks that get a stem: those with clips that are not muted, and each bus that an unmuted track sends into.</summary>
    public static IReadOnlyList<TrackId> StemTracks(ProjectDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        bool Fed(Track bus) => document.Tracks.Any(track =>
            track.Kind != TrackKind.Bus
            && !MixRules.IsMuted(track, document.Groups)
            && track.Sends.Any(send => send.Bus == bus.Id && !send.Muted));
        return
        [
            .. document.Tracks
                .Where(track => !MixRules.IsMuted(track, document.Groups))
                .Where(track => track.Kind == TrackKind.Bus ? Fed(track) : track.Clips.Count > 0 || track.MidiClips.Count > 0)
                .Select(track => track.Id),
        ];
    }

    /// <summary>Renders the stems of <paramref name="tracks"/>, or of <see cref="StemTracks"/> when none are named.</summary>
    public static IReadOnlyList<Stem> Render(
        ProjectDocument document,
        IMediaSource media,
        OfflineOptions? options = null,
        IReadOnlyList<TrackId>? tracks = null)
    {
        (OfflineOptions settings, List<(Track Track, RenderPlan Plan)> plans) = Prepare(document, media, options, tracks);
        return [.. plans.Select(entry => new Stem(entry.Track.Id, entry.Track.Name, OfflineRenderer.Render(entry.Plan, settings)))];
    }

    /// <summary>Writes the stems to a ZIP of WAV files, one entry for each, named by their place and the track's name.</summary>
    public static void WriteZip(
        ProjectDocument document,
        IMediaSource media,
        Stream output,
        WavOptions wav,
        OfflineOptions? options = null,
        IReadOnlyList<TrackId>? tracks = null)
    {
        ArgumentNullException.ThrowIfNull(wav);
        (OfflineOptions settings, List<(Track Track, RenderPlan Plan)> plans) = Prepare(document, media, options, tracks);
        // The stored timestamp is fixed, so the same project always gives the same file.
        DateTimeOffset stamp = new(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);
        using ZipArchive archive = new(output, ZipArchiveMode.Create, leaveOpen: true);
        for (int i = 0; i < plans.Count; i++)
        {
            ZipArchiveEntry entry = archive.CreateEntry(FileName(i + 1, plans[i].Track.Name), CompressionLevel.NoCompression);
            entry.LastWriteTime = stamp;
            using Stream stream = entry.Open();
            WavExporter.Write(plans[i].Plan, stream, wav, settings);
        }
    }

    /// <summary>The name of a stem's file: its place in the export and the track's name with anything a file name cannot hold replaced.</summary>
    public static string FileName(int number, string trackName)
    {
        ArgumentNullException.ThrowIfNull(trackName);
        string safe = new string([.. trackName.Select(CleanCharacter)]).Trim();
        if (safe.Length > MaxFileNameLength)
        {
            safe = safe[..MaxFileNameLength].TrimEnd();
        }
        return $"{number:00} {(safe.Length == 0 ? $"Track {number}" : safe)}.wav";
    }

    private static char CleanCharacter(char character) =>
        character is '\\' or '/' or ':' or '*' or '?' or '"' or '<' or '>' or '|' || char.IsControl(character) ? '_' : character;

    private static (OfflineOptions Settings, List<(Track Track, RenderPlan Plan)> Plans) Prepare(
        ProjectDocument document,
        IMediaSource media,
        OfflineOptions? options,
        IReadOnlyList<TrackId>? tracks)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(media);
        OfflineOptions settings = OfflineRenderer.Validate(options);
        MediaCache cache = new(media, settings.SampleRate);
        List<(Track Track, RenderPlan Plan)> plans = [];
        foreach (TrackId id in tracks ?? StemTracks(document))
        {
            Track track = document.Tracks.FirstOrDefault(candidate => candidate.Id == id)
                ?? throw new ArgumentException($"No track {id} to make a stem of.", nameof(tracks));
            PlanOptions plan = OfflineRenderer.PlanOptionsOf(settings with { StemOf = id }) with { Cache = cache };
            plans.Add((track, RenderPlan.Create(document, media, plan)));
        }
        long tail = plans.Count == 0 ? 0 : plans.Max(entry => entry.Plan.TailFrames);
        return (settings with { TailFrames = tail }, plans);
    }
}
