using AudioLab.Core.Model;

namespace AudioLab.Server.Media;

/// <summary>What the header of a checked WAV file says and where its samples are.</summary>
/// <param name="Encoding">How the samples are stored.</param>
/// <param name="Channels">Interleaved channels in a frame.</param>
/// <param name="SampleRate">Frames a second.</param>
/// <param name="BlockAlign">Bytes in a frame.</param>
/// <param name="DataOffset">Where the samples start in the file.</param>
/// <param name="DataBytes">How many bytes of samples there are, a whole number of frames.</param>
public readonly record struct WavFormat(SampleEncoding Encoding, int Channels, int SampleRate, int BlockAlign, long DataOffset, long DataBytes)
{
    /// <summary>How many frames the file holds.</summary>
    public long Frames => DataBytes / BlockAlign;

    /// <summary>The length of the file in seconds.</summary>
    public double Duration => (double)Frames / SampleRate;
}
