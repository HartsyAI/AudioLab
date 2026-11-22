namespace AudioLab.Audio;

/// <summary>A sound source of an instrument track, such as a synth or a sampler playing the track's MIDI clips.</summary>
/// <remarks>Real-time rules apply to <see cref="Render"/>.</remarks>
public interface ITrackSource
{
    /// <summary>How long the source keeps sounding after its last note, in seconds.</summary>
    public double TailSeconds { get; }

    /// <summary>Stops every voice and forgets all state, as when the playhead jumps.</summary>
    public void Reset();

    /// <summary>Adds a block to the two buffers; <paramref name="startFrame"/> is the timeline frame of its first sample.</summary>
    public void Render(long startFrame, Span<float> left, Span<float> right);
}
