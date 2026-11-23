namespace AudioLab.Server.Media;

/// <summary>What a quota lets one user keep.</summary>
/// <param name="MaxFiles">The most files.</param>
/// <param name="MaxBytes">The most bytes of media.</param>
public readonly record struct MediaQuotaLimits(int MaxFiles, long MaxBytes);
