namespace AudioLab.Core.Model;

/// <summary>A convolution reverb built from a generated impulse response.</summary>
public sealed record ReverbEffect : Effect
{
    /// <summary>The impulse response length, in seconds.</summary>
    public required double Size { get; init; }

    /// <summary>How fast the tail decays.</summary>
    public required double Decay { get; init; }

    /// <summary>The delay before the tail, in seconds.</summary>
    public required double Predelay { get; init; }

    /// <summary>The wet share, 0 to 1.</summary>
    public required double Mix { get; init; }
}
