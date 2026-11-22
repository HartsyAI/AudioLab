using AudioLab.Core.Model;

namespace AudioLab.Audio;

/// <summary>How a render plan is prepared.</summary>
public sealed record PlanOptions
{
    /// <summary>The rate to render at, in hertz.</summary>
    public required int SampleRate { get; init; }

    /// <summary>Builds the sources of instrument tracks; without it those tracks are silent.</summary>
    public ITrackSourceFactory? Sources { get; init; }

    /// <summary>Replaces how effects become inserts, so a test can run its own insert.</summary>
    internal Func<Effect, InsertContext, IInsert?>? InsertBuilder { get; init; }
}
