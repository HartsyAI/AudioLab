namespace AudioLab.Audio;

/// <summary>What was found while a file was written.</summary>
/// <param name="ClippedSamples">Samples pulled back to the edge of the integer range because they went past it.</param>
/// <param name="NonFiniteSamples">Samples that were NaN or infinite, which an integer file holds as silence and a float file as they were.</param>
public readonly record struct WavResult(long ClippedSamples, long NonFiniteSamples)
{
    /// <summary>Whether anything was clipped or not finite, which is a fault of the mix or of a plug-in to report.</summary>
    public bool HasProblems => ClippedSamples > 0 || NonFiniteSamples > 0;
}
