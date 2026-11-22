namespace AudioLab.Audio;

/// <summary>The noise added before a float sample is rounded to an integer, which turns the rounding error into steady noise.</summary>
public enum DitherKind
{
    /// <summary>No noise: the rounding error follows the signal and is audible on quiet material.</summary>
    None,

    /// <summary>Noise spread evenly over one step of the integer scale.</summary>
    Rectangular,

    /// <summary>Noise with a triangular spread over two steps, which makes the error independent of the signal.</summary>
    Triangular,
}
