using AudioLab.Core.Model;

namespace AudioLab.Audio;

/// <summary>What an offline render covers and how it is cut into blocks.</summary>
public sealed record OfflineOptions
{
    /// <summary>The rate to render at, in hertz.</summary>
    public int SampleRate { get; init; } = 48000;

    /// <summary>Where the render starts on the timeline, in seconds.</summary>
    public double Start { get; init; }

    /// <summary>Where the content ends, in seconds; the end of the last clip when null.</summary>
    public double? End { get; init; }

    /// <summary>Whether to run on past the end until effects and instruments have rung out.</summary>
    public bool IncludeTail { get; init; } = true;

    /// <summary>The block size the mixer works in; the output does not depend on it.</summary>
    public int BlockFrames { get; init; } = MixerGraph.DefaultBlockFrames;

    /// <summary>Builds the sources of instrument tracks; without it those tracks are silent.</summary>
    public ITrackSourceFactory? Sources { get; init; }

    /// <summary>Renders the stem of this track instead of the mix; see <see cref="PlanOptions.StemOf"/>.</summary>
    public TrackId? StemOf { get; init; }

    /// <summary>Overrides how many frames the render runs on past its end, so that several renders can be the same length.</summary>
    public long? TailFrames { get; init; }
}
