using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Audio;

/// <summary>A document compiled for rendering: mute, solo and gains settled by <see cref="MixRules"/>, media resolved and resampled.</summary>
/// <remarks>
/// Preparing allocates and may throw; rendering from the plan does neither. A plan owns its inserts and sources, which hold state, so one
/// <see cref="MixerGraph"/> at a time may run it.
/// </remarks>
public sealed class RenderPlan
{
    private RenderPlan(
        AudioFormat format,
        PlannedTrack[] tracks,
        double masterGain,
        bool limiter,
        long contentFrames,
        long tailFrames,
        int busCount,
        bool tailIsCut)
    {
        Format = format;
        Tracks = tracks;
        MasterGain = (float)masterGain;
        Limiter = limiter;
        ContentFrames = contentFrames;
        TailFrames = tailFrames;
        BusCount = busCount;
        TailIsCut = tailIsCut;
    }

    /// <summary>The format the plan renders: stereo at the plan's sample rate.</summary>
    public AudioFormat Format { get; }

    /// <summary>Where the last clip ends, in frames from the start of the project.</summary>
    public long ContentFrames { get; }

    /// <summary>How long the effects and instruments ring on after the content, in frames.</summary>
    public long TailFrames { get; }

    /// <summary>Whether a tail was cut short at its limit, so that a render of it should fade out.</summary>
    public bool TailIsCut { get; }

    internal PlannedTrack[] Tracks { get; }

    internal int BusCount { get; }

    internal float MasterGain { get; }

    internal bool Limiter { get; }

    /// <summary>Prepares a document to render. Throws <see cref="MissingMediaException"/> when an audible clip's media is not available.</summary>
    public static RenderPlan Create(ProjectDocument document, IMediaSource media, PlanOptions options)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(media);
        ArgumentNullException.ThrowIfNull(options);
        AudioFormat format = new(options.SampleRate, 2);
        InsertContext context = new(format.SampleRate, TempoSteps(document, format.SampleRate));
        Dictionary<TrackId, int> busSlots = [];
        foreach (Track track in document.Tracks.Where(track => track.Kind == TrackKind.Bus))
        {
            busSlots[track.Id] = busSlots.Count;
        }
        MediaCache shared = options.Cache ?? new MediaCache(media, format.SampleRate);
        if (shared.SampleRate != format.SampleRate)
        {
            throw new ArgumentException("The media cache is at another sample rate than the plan.", nameof(options));
        }
        PlanMedia cache = new(shared);
        List<PlannedTrack> planned = [];
        // Tracks first, then buses, so a send always finds the bus it feeds after the audio has been mixed into it.
        IEnumerable<Track> ordered = document.Tracks.OrderBy(track => track.Kind == TrackKind.Bus);
        foreach (Track track in ordered)
        {
            PlannedTrack? plan = PlanTrack(document, track, busSlots, cache, context, options);
            if (plan is not null)
            {
                planned.Add(plan);
            }
        }
        cache.ThrowIfMissing();
        double contentEnd = ProjectContent.End(document, audibleOnly: true);
        return new RenderPlan(
            format,
            [.. planned],
            document.MasterVolume,
            document.MasterLimiter,
            Frames(contentEnd, format.SampleRate),
            Frames(TailSeconds(planned, busSlots.Count), format.SampleRate),
            busSlots.Count,
            planned.Any(track => track.Inserts.Any(insert => insert.TailIsCut)));
    }

    // The tempo in force from each frame a tempo change falls on, so an effect can follow it without a search of the map.
    private static TempoStep[] TempoSteps(ProjectDocument document, int sampleRate)
    {
        TempoMap tempo = TempoMap.From(document);
        List<TempoStep> steps = [new TempoStep(0, tempo.BpmAt(0))];
        IEnumerable<TempoChange> changes = document.TempoMap
            .Where(change => double.IsFinite(change.Time) && double.IsFinite(change.Bpm) && change.Time > 1e-6 && change.Bpm > 0)
            .OrderBy(change => change.Time);
        foreach (TempoChange change in changes)
        {
            steps.Add(new TempoStep(Frames(change.Time, sampleRate), tempo.BpmAt(change.Time)));
        }
        return [.. steps];
    }

    // Plain rounding, so a clip lands on the same frame whichever way it is reached.
    internal static long Frames(double seconds, int sampleRate) => (long)Math.Round(seconds * sampleRate, MidpointRounding.AwayFromZero);

    private static PlannedTrack? PlanTrack(
        ProjectDocument document,
        Track track,
        Dictionary<TrackId, int> busSlots,
        PlanMedia cache,
        InsertContext context,
        PlanOptions options)
    {
        bool muted = MixRules.IsMuted(track, document.Groups);
        bool soloAudible = MixRules.IsSoloAudible(document, track);
        List<PlannedSend> sends = [];
        foreach (Send send in track.Sends)
        {
            float gain = (float)MixRules.SendGain(document, track, send);
            if (gain > 0 && busSlots.TryGetValue(send.Bus, out int slot))
            {
                sends.Add(new PlannedSend(slot, gain, send.Pre));
            }
        }
        // A track silent under solo still runs when a send of it is heard; a muted one has no audible send, so it never runs.
        if (!(!muted && soloAudible) && sends.Count == 0)
        {
            return null;
        }
        int sampleRate = context.SampleRate;
        bool automated = track.Automation.Mode != AutomationMode.Off;
        (PlannedClip[] clips, long[] latestEnds) = track.Kind == TrackKind.Audio
            ? PlanClips(track, cache, sampleRate)
            : (Array.Empty<PlannedClip>(), Array.Empty<long>());
        ITrackSource? source = track.Kind == TrackKind.Instrument
            ? options.Sources?.Create(document, track, new AudioFormat(sampleRate, 2), cache)
            : null;
        return new PlannedTrack
        {
            BusSlot = track.Kind == TrackKind.Bus ? busSlots[track.Id] : -1,
            FaderGain = (float)MixRules.TrackLevel(track, document.Groups),
            Pan = track.Pan,
            Mono = IsMono(track, clips, source),
            SoloAudible = soloAudible,
            VolumeLane = automated && track.Automation.Volume.Count > 0 ? track.Automation.Volume : null,
            PanLane = automated && track.Automation.Pan.Count > 0 ? track.Automation.Pan : null,
            Sends = [.. sends],
            Inserts = [.. track.Effects.Select(effect => (options.InsertBuilder ?? InsertFactory.Create)(effect, context)).OfType<IInsert>()],
            Clips = clips,
            LatestEnds = latestEnds,
            Source = source,
        };
    }

    // A track that is the same on both channels is panned by the mono law; a reverb gives each channel its own room.
    private static bool IsMono(Track track, PlannedClip[] clips, ITrackSource? source)
    {
        if (track.Kind == TrackKind.Bus || track.Effects.Any(effect => effect.Enabled && effect is ReverbEffect))
        {
            return false;
        }
        return track.Kind == TrackKind.Instrument ? source?.IsMono == true : clips.All(clip => clip.Audio.ChannelCount == 1);
    }

    private static (PlannedClip[] Clips, long[] LatestEnds) PlanClips(Track track, PlanMedia cache, int sampleRate)
    {
        Dictionary<ClipId, AudioClip> byId = [];
        foreach (AudioClip clip in track.Clips)
        {
            if (!byId.TryAdd(clip.Id, clip))
            {
                throw new ArgumentException($"Track {track.Id} has two clips with the id {clip.Id}; the document is not valid.", "document");
            }
        }
        List<PlannedClip> clips = [];
        foreach (AudibleSpan span in LayerRules.AudibleSpans(track.Clips))
        {
            AudioClip clip = byId[span.Clip];
            PcmAudio? audio = cache.Get(clip.Media);
            if (audio is null)
            {
                continue;
            }
            long clipStart = Frames(clip.Start, sampleRate);
            long clipEnd = Frames(ClipRules.EndOf(clip), sampleRate);
            double visible = ClipRules.VisibleLength(clip);
            double fadeIn = Math.Min(clip.FadeIn, visible);
            double fadeOut = Math.Min(clip.FadeOut, visible - fadeIn);
            clips.Add(new PlannedClip(
                audio,
                (clipStart, clipEnd),
                (Frames(span.Start, sampleRate), Frames(span.End, sampleRate)),
                Frames(clip.Offset, sampleRate),
                (Frames(fadeIn, sampleRate), Frames(fadeOut, sampleRate)),
                (span.SeamIn, span.SeamOut, Math.Max(1, Frames(LayerRules.SeamFade, sampleRate))),
                (float)clip.Gain));
        }
        PlannedClip[] ordered = [.. clips.OrderBy(clip => clip.SpanStart)];
        long[] latest = new long[ordered.Length];
        long end = long.MinValue;
        for (int i = 0; i < ordered.Length; i++)
        {
            end = Math.Max(end, ordered[i].SpanEnd);
            latest[i] = end;
        }
        return (ordered, latest);
    }

    // A track rings through its own inserts and then through the inserts of the buses it feeds.
    private static double TailSeconds(List<PlannedTrack> planned, int busCount)
    {
        double[] busTails = new double[busCount];
        foreach (PlannedTrack bus in planned.Where(track => track.IsBus))
        {
            busTails[bus.BusSlot] = OwnTail(bus);
        }
        double longest = 0;
        foreach (PlannedTrack track in planned)
        {
            double fed = track.IsBus ? 0 : track.Sends.Select(send => busTails[send.BusSlot]).DefaultIfEmpty(0).Max();
            longest = Math.Max(longest, OwnTail(track) + fed);
        }
        return longest;
    }

    private static double OwnTail(PlannedTrack track) => track.Inserts.Sum(insert => insert.TailSeconds) + (track.Source?.TailSeconds ?? 0);
}
