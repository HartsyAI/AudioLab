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

    /// <summary>Shares the decoded and resampled media of other plans made at the same rate.</summary>
    internal MediaCache? Cache { get; init; }

    /// <summary>Replaces how effects become inserts, so a test can run its own insert.</summary>
    internal Func<Effect, InsertContext, IInsert?>? InsertBuilder { get; init; }
}
