namespace AudioLab.Server.Media;

/// <summary>What one user has stored, as a quota sees it.</summary>
/// <param name="Files">How many media files.</param>
/// <param name="Bytes">Their size in bytes.</param>
public readonly record struct MediaUsage(int Files, long Bytes);
