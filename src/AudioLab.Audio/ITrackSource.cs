namespace AudioLab.Audio;

/// <summary>A sound source of an instrument track, such as a synth or a sampler playing the track's MIDI clips.</summary>
/// <remarks>
/// Real-time rules apply to <see cref="Render"/>. Blocks are consecutive frames unless <see cref="Reset"/> was called in between,
/// and the output must not depend on how they are cut.
/// </remarks>
public interface ITrackSource
{
    /// <summary>Whether the source puts the same sound on both channels, which decides the pan law of its track.</summary>
    public bool IsMono { get; }

    /// <summary>How long the source keeps sounding after its last note, in seconds.</summary>
    public double TailSeconds { get; }

    /// <summary>Stops every voice and forgets all state, as when the playhead jumps.</summary>
    public void Reset();

    /// <summary>Adds a block to the two buffers; <paramref name="startFrame"/> is the timeline frame of its first sample.</summary>
    public void Render(long startFrame, Span<float> left, Span<float> right);
}
