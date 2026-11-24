using AudioLab.Core.Model;

namespace AudioLab.Audio;

/// <summary>How a render plan is prepared.</summary>
public sealed record PlanOptions
{
    /// <summary>The rate to render at, in hertz.</summary>
    public required int SampleRate { get; init; }

    /// <summary>Builds the sources of instrument tracks; without it those tracks are silent.</summary>
    public ITrackSourceFactory? Sources { get; init; }

    /// <summary>Renders only the stem of this track: a track alone and dry, or a bus with what the tracks send into it.</summary>
    /// <remarks>Solo is ignored, the master volume is at unity and there is no limiter, so the stems of a project add up to its mix.</remarks>
    public TrackId? StemOf { get; init; }

    /// <summary>Shares decoded, cleaned and resampled media between plans; its rate must be the plan's, and the plan's source is not asked.</summary>
    public MediaCache? Cache { get; init; }

    /// <summary>Stops the preparation of the plan, which decodes and converts media, when it is cancelled.</summary>
    public CancellationToken CancellationToken { get; init; }

    /// <summary>Whether the plan is only for its lengths: effects are built to report their tails and latency and make no sound.</summary>
    internal bool SizingOnly { get; init; }

    /// <summary>Replaces how effects become inserts, so a test can run its own insert.</summary>
    internal Func<Effect, InsertContext, IInsert?>? InsertBuilder { get; init; }
}
