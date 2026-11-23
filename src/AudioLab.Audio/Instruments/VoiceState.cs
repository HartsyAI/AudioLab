namespace AudioLab.Audio.Instruments;

/// <summary>Where a synth voice is in its life.</summary>
internal enum VoiceState
{
    /// <summary>Not sounding; free to take a note.</summary>
    Free,

    /// <summary>Its note is held: in the attack, the decay or the sustain.</summary>
    Held,

    /// <summary>Its note has been let go and it is dying away.</summary>
    Released,

    /// <summary>It was taken over by a newer note and is fading out quickly; it no longer counts against the polyphony.</summary>
    Fading,
}
