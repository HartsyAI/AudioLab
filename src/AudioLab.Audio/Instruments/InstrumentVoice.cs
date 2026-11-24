namespace AudioLab.Audio.Instruments;

/// <summary>One note of an instrument as <see cref="PolyphonicSource"/> sees it: it starts, plays, is let go or taken over, and ends.</summary>
/// <remarks>A voice allocates nothing after it is built and depends only on the frames since its own start, never on the block.</remarks>
internal abstract class InstrumentVoice
{
    /// <summary>Where the voice is in its life.</summary>
    public VoiceState State { get; protected set; }

    /// <summary>The frame the voice started on, which is how old it is against another voice.</summary>
    public long StartFrame { get; protected set; }

    /// <summary>How loud the voice is now, so that the quietest of the dying ones can be taken over.</summary>
    public abstract double Level { get; }

    /// <summary>Starts a note at <paramref name="startFrame"/>, to be let go at <paramref name="offFrame"/>.</summary>
    /// <param name="startFrame">The timeline frame of the voice's first sample.</param>
    /// <param name="offFrame">The frame the note is let go on.</param>
    /// <param name="pitch">The MIDI note number.</param>
    /// <param name="velocity">The strength, from 0.05 to 1.</param>
    /// <param name="elapsed">How many frames of the note were over when the voice starts, as after a seek; 0 for a note that starts.</param>
    public abstract void Start(long startFrame, long offFrame, int pitch, float velocity, long elapsed);

    /// <summary>Lets a voice that is taken over for a newer note fade out over a few milliseconds instead of cutting off.</summary>
    public abstract void Take();

    /// <summary>Adds <paramref name="count"/> frames, which begin at timeline frame <paramref name="startFrame"/>, to both channels.</summary>
    public abstract void Render(long startFrame, int count, Span<float> left, Span<float> right);

    /// <summary>Stops the voice at once and frees it.</summary>
    public void Stop() => State = VoiceState.Free;
}
