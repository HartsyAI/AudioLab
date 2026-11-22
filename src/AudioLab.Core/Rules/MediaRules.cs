using System.Globalization;
using AudioLab.Core.Commands;
using AudioLab.Core.Model;

namespace AudioLab.Core.Rules;

/// <summary>What a document may say about media files: they exist, are the owner's, and a clip's duration is the file's.</summary>
/// <remarks>
/// One rule serves every command that names a media file, now or later: it compares the media a result references with the
/// document it comes from, so no command needs its own check. Core holds no records; the catalog is the server's.
/// </remarks>
public static class MediaRules
{
    /// <summary>How far a clip's duration may differ from its media file's: one sample period at the file's rate, plus a nanosecond.</summary>
    public static double DurationTolerance(MediaRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return (1.0 / record.SampleRate) + ProjectLimits.Tolerance;
    }

    /// <summary>The refusal for a media file that is missing and for one that is another user's, which must not differ (D12).</summary>
    public const string NotFoundMessage = "There is no such media file.";

    /// <summary>Why <paramref name="after"/> may not replace <paramref name="before"/>, or null when it may.</summary>
    /// <remarks>
    /// A media file that <paramref name="before"/> does not already reference must be found, as the owner's, by
    /// <see cref="IMediaCatalog.Find"/>. A clip that is new or whose media or duration changed must have the file's duration within
    /// <see cref="DurationTolerance"/> at the file's own rate. A file that the document already names may be mid-delete, which only hides it from new
    /// references, so it is looked up with <see cref="IMediaCatalog.FindIncludingPendingDelete"/>.
    /// </remarks>
    public static CommandError? VerifyChange(ProjectDocument before, ProjectDocument after, IMediaCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        ArgumentNullException.ThrowIfNull(catalog);
        IReadOnlySet<MediaId> known = ProjectContent.ReferencedMedia(before);
        foreach (MediaId media in ProjectContent.ReferencedMedia(after))
        {
            if (!known.Contains(media) && catalog.Find(after.Owner, media) is null)
            {
                return new CommandError(CommandErrorCode.NotFound, NotFoundMessage);
            }
        }
        Dictionary<ClipId, AudioClip> previous = before.Tracks.SelectMany(track => track.Clips).ToDictionary(clip => clip.Id);
        foreach (AudioClip clip in after.Tracks.SelectMany(track => track.Clips))
        {
            if (previous.TryGetValue(clip.Id, out AudioClip? old) && old.Media == clip.Media && old.Duration == clip.Duration)
            {
                continue;
            }
            MediaRecord? record = catalog.Find(after.Owner, clip.Media)
                ?? (known.Contains(clip.Media) ? catalog.FindIncludingPendingDelete(after.Owner, clip.Media) : null);
            if (record is null)
            {
                return new CommandError(CommandErrorCode.NotFound, NotFoundMessage);
            }
            if (DurationProblem(clip, record) is { } problem)
            {
                return new CommandError(CommandErrorCode.Rejected, problem);
            }
        }
        return null;
    }

    /// <summary>Every way a whole document breaks the rule, for one that was loaded rather than made by a command; empty when none.</summary>
    /// <remarks>A file mid-delete still counts as there: opening a project must not fail because a delete is about to be refused.</remarks>
    public static IReadOnlyList<string> DocumentProblems(ProjectDocument document, IMediaCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(catalog);
        ProblemList problems = new();
        foreach (Track track in document.Tracks)
        {
            foreach (AudioClip clip in track.Clips)
            {
                MediaRecord? record = catalog.FindIncludingPendingDelete(document.Owner, clip.Media);
                if (record is null)
                {
                    problems.Add($"clip {clip.Id.Value}: {NotFoundMessage}");
                }
                else if (DurationProblem(clip, record) is { } problem)
                {
                    problems.Add($"clip {clip.Id.Value}: {problem}");
                }
            }
            SamplerInstrument? sampler = track.Instrument as SamplerInstrument;
            if (sampler?.Sample is { } sample && catalog.FindIncludingPendingDelete(document.Owner, sample) is null)
            {
                problems.Add($"track {track.Id.Value}: sampler: {NotFoundMessage}");
            }
        }
        foreach (BeatLane lane in document.BeatPattern.Lanes)
        {
            if (lane.Media is { } media && catalog.FindIncludingPendingDelete(document.Owner, media) is null)
            {
                problems.Add($"lane {lane.Id.Value}: {NotFoundMessage}");
            }
        }
        return problems.ToList();
    }

    private static string? DurationProblem(AudioClip clip, MediaRecord record)
    {
        double tolerance = DurationTolerance(record);
        if (Math.Abs(clip.Duration - record.Duration) <= tolerance)
        {
            return null;
        }
        string given = clip.Duration.ToString("R", CultureInfo.InvariantCulture);
        string actual = record.Duration.ToString("R", CultureInfo.InvariantCulture);
        return $"the clip's duration of {given} s is not the media file's {actual} s, within one sample period ({tolerance:G3} s).";
    }
}
