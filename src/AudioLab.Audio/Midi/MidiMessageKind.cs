namespace AudioLab.Audio.Midi;

/// <summary>The events of a track that the codec acts on; every other event is read, checked and left out.</summary>
internal enum MidiMessageKind
{
    /// <summary>A note starts; the velocity is 1 to 127.</summary>
    NoteOn,

    /// <summary>A note ends, or starts with velocity 0.</summary>
    NoteOff,

    /// <summary>A controller changes.</summary>
    Controller,

    /// <summary>The tempo changes; the first datum is microseconds a quarter note.</summary>
    Tempo,

    /// <summary>The time signature changes; the data are the numerator and the power of two of the denominator.</summary>
    TimeSignature,

    /// <summary>The track ends.</summary>
    EndOfTrack,

    /// <summary>An event with no part in a clip, such as a program change, a pitch bend or a text.</summary>
    Ignored,
}
