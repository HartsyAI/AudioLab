namespace AudioLab.Core.Model;

/// <summary>A user's media files, newest first, and how many stored records were left out as invalid.</summary>
/// <param name="Files">The files.</param>
/// <param name="Refused">How many stored records failed their checks and are not listed.</param>
public sealed record MediaListing(IReadOnlyList<MediaSummary> Files, int Refused);
