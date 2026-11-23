namespace AudioLab.Audio.Midi;

/// <summary>A MIDI file that cannot be read, or a clip that cannot be written to one, with the reason.</summary>
public sealed class MidiFormatException : Exception
{
    /// <summary>Creates the exception with the reason.</summary>
    public MidiFormatException(string message)
        : base(message)
    {
    }
}
