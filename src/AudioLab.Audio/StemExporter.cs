using System.Globalization;
using System.Text;
using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Audio;

/// <summary>Renders each track, and each aux bus that is fed, on its own, so that the stems add up to the mix.</summary>
/// <remarks>
/// A track stem is the track alone and dry; a bus stem is what the tracks send into it. Solo is ignored, the master volume is at unity
/// and there is no limiter, and a muted track gives a silent stem, as it adds nothing to the mix. Every stem runs to the same length,
/// that of the longest tail among them. Everything that can fail beforehand (missing media, a stem or the archive too large) fails before
/// the first byte is written; a failure after that leaves a ZIP file without its central directory, which no reader takes as complete,
/// and the caller discards it. The asynchronous member is for a server, which passes its response body and the request's token.
/// </remarks>
public static class StemExporter
{
    private const int MaxFileNameLength = 100;

    /// <summary>The tracks that get a stem: those with a clip that is not muted, and each bus that an unmuted track sends into.</summary>
    public static IReadOnlyList<TrackId> StemTracks(ProjectDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        bool Fed(Track bus) => document.Tracks.Any(track =>
            track.Kind != TrackKind.Bus
            && !MixRules.IsMuted(track, document.Groups)
            && track.Sends.Any(send => send.Bus == bus.Id && !send.Muted));
        bool HasSound(Track track) => track.Clips.Any(clip => !clip.Muted) || track.MidiClips.Any(clip => !clip.Muted);
        return
        [
            .. document.Tracks
                .Where(track => !MixRules.IsMuted(track, document.Groups))
                .Where(track => track.Kind == TrackKind.Bus ? Fed(track) : HasSound(track))
                .Select(track => track.Id),
        ];
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
        ArgumentNullException.ThrowIfNull(output);
        Prepared prepared = Prepare(document, media, options, tracks);
        Preflight(prepared, wav);
        ZipWriter zip = new(output);
        for (int i = 0; i < prepared.Stems.Count; i++)
        {
            RenderPlan plan = PlanOf(document, media, prepared, prepared.Stems[i].Track.Id);
            using Stream entry = zip.StartEntry(prepared.Names[i]);
            WavExporter.Write(plan, entry, SeededFor(wav, i), prepared.Settings);
            zip.FinishEntry();
        }
        zip.Finish();
    }

    /// <summary>Writes the stems to a stream that takes asynchronous writes, and stops when the token is cancelled.</summary>
    public static async Task WriteZipAsync(
        ProjectDocument document,
        IMediaSource media,
        Stream output,
        WavOptions wav,
        OfflineOptions? options = null,
        IReadOnlyList<TrackId>? tracks = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(wav);
        ArgumentNullException.ThrowIfNull(output);
        Prepared prepared = Prepare(document, media, options, tracks);
        Preflight(prepared, wav);
        ZipWriter zip = new(output);
        for (int i = 0; i < prepared.Stems.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RenderPlan plan = PlanOf(document, media, prepared, prepared.Stems[i].Track.Id);
            Stream entry = await zip.StartEntryAsync(prepared.Names[i], cancellationToken).ConfigureAwait(false);
            await using (entry.ConfigureAwait(false))
            {
                await WavExporter.WriteAsync(plan, entry, SeededFor(wav, i), prepared.Settings, cancellationToken).ConfigureAwait(false);
            }
            await zip.FinishEntryAsync(cancellationToken).ConfigureAwait(false);
        }
        await zip.FinishAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The name of a stem's file: its place in the export and the track's name, without what a file name cannot hold.</summary>
    /// <remarks>
    /// Control and format characters (the ones that reorder text among them) and the characters of a path become underscores, and a long
    /// name is cut at a whole character of text, never inside a surrogate pair or a combining sequence.
    /// </remarks>
    public static string FileName(int number, string trackName)
    {
        ArgumentNullException.ThrowIfNull(trackName);
        string cleaned = new(trackName.Select(CleanCharacter).ToArray());
        string safe = Cut(cleaned.Trim(), MaxFileNameLength).TrimEnd();
        return $"{number:00} {(safe.Length == 0 ? $"Track {number}" : safe)}.wav";
    }

    /// <summary>Renders the stems of <paramref name="tracks"/>, or of <see cref="StemTracks"/> when none are named, into memory.</summary>
    /// <remarks>For tests and short renders: the whole of every stem is held at once. Export writes them with <see cref="WriteZip"/>.</remarks>
    internal static IReadOnlyList<Stem> Render(
        ProjectDocument document,
        IMediaSource media,
        OfflineOptions? options = null,
        IReadOnlyList<TrackId>? tracks = null)
    {
        Prepared prepared = Prepare(document, media, options, tracks);
        List<Stem> stems = [];
        foreach ((Track track, _) in prepared.Stems)
        {
            RenderPlan plan = PlanOf(document, media, prepared, track.Id);
            stems.Add(new Stem(track.Id, track.Name, OfflineRenderer.Render(plan, prepared.Settings)));
        }
        return stems;
    }

    private static char CleanCharacter(char character) =>
        character is '\\' or '/' or ':' or '*' or '?' or '"' or '<' or '>' or '|'
        || char.GetUnicodeCategory(character) is UnicodeCategory.Control or UnicodeCategory.Format
            ? '_'
            : character;

    private static string Cut(string text, int length)
    {
        if (text.Length <= length)
        {
            return text;
        }
        StringBuilder cut = new();
        TextElementEnumerator elements = StringInfo.GetTextElementEnumerator(text);
        while (elements.MoveNext())
        {
            string element = elements.GetTextElement();
            if (cut.Length + element.Length > length)
            {
                break;
            }
            cut.Append(element);
        }
        return cut.ToString();
    }

    private static WavOptions SeededFor(WavOptions wav, int index) => wav with { Seed = wav.Seed + (uint)index };

    // The stems and the options they all share: the cache of media, and a tail as long as the longest one.
    private static Prepared Prepare(ProjectDocument document, IMediaSource media, OfflineOptions? options, IReadOnlyList<TrackId>? tracks)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(media);
        OfflineOptions given = OfflineRenderer.Validate(options);
        MediaCache cache = given.Cache ?? new MediaCache(media, given.SampleRate);
        OfflineOptions settings = given with { Cache = cache };
        List<(Track Track, RenderPlan Sizing)> stems = [];
        foreach (TrackId id in tracks ?? StemTracks(document))
        {
            Track track = document.Tracks.FirstOrDefault(candidate => candidate.Id == id)
                ?? throw new ArgumentException($"No track {id} to make a stem of.", nameof(tracks));
            // Only the lengths are wanted here, so no effect is built: a project with a reverb on every track must not hold them all at once.
            PlanOptions sizing = OfflineRenderer.PlanOptionsOf(settings with { StemOf = id }) with { SizingOnly = true };
            stems.Add((track, RenderPlan.Create(document, media, sizing)));
        }
        long tail = stems.Count == 0 ? 0 : stems.Max(entry => entry.Sizing.TailFrames);
        string[] names = [.. stems.Select((entry, i) => FileName(i + 1, entry.Track.Name))];
        return new Prepared(settings with { TailFrames = settings.TailFrames ?? tail }, stems, names);
    }

    private static RenderPlan PlanOf(ProjectDocument document, IMediaSource media, Prepared prepared, TrackId id) =>
        RenderPlan.Create(document, media, OfflineRenderer.PlanOptionsOf(prepared.Settings with { StemOf = id }));

    // Checks the sizes of the whole archive, so that nothing is written for an export that cannot be finished.
    private static void Preflight(Prepared prepared, WavOptions wav)
    {
        if (prepared.Stems.Count > ZipWriter.MaxEntries)
        {
            throw new ArgumentOutOfRangeException(nameof(prepared), "There are more stems than a ZIP file without ZIP64 holds.");
        }
        long total = ZipWriter.EndRecordSize;
        for (int i = 0; i < prepared.Stems.Count; i++)
        {
            (_, long frames) = OfflineRenderer.RangeOf(prepared.Stems[i].Sizing, prepared.Settings);
            string name = prepared.Names[i];
            total += WavStreamWriter.SizeOf(frames, wav) + ZipWriter.EntryOverhead(name) + ZipWriter.DirectoryOverhead(name);
        }
        if (total > uint.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(prepared), "The stems would make a ZIP file of more than 4 GiB.");
        }
    }

    private sealed record Prepared(OfflineOptions Settings, List<(Track Track, RenderPlan Sizing)> Stems, string[] Names);
}
