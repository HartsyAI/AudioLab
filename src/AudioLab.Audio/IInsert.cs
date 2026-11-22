namespace AudioLab.Audio;

/// <summary>One insert effect running on a track's stereo signal, before the fader.</summary>
/// <remarks>Real-time rules apply to <see cref="Process"/>: no allocation, locks, logging, I/O or exceptions.</remarks>
public interface IInsert
{
    /// <summary>How long the effect keeps sounding after its input stops, in seconds, so an export can run on until it has.</summary>
    public double TailSeconds { get; }

    /// <summary>Forgets everything the effect holds from earlier audio.</summary>
    public void Reset();

    /// <summary>Processes a block in place. <paramref name="startFrame"/> is the timeline frame of its first sample.</summary>
    public void Process(Span<float> left, Span<float> right, long startFrame);
}
