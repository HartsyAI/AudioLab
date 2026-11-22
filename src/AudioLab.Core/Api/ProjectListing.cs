namespace AudioLab.Core.Api;

/// <summary>A user's projects, and how many stored files the store refused to list because they are not valid.</summary>
/// <param name="Projects">The valid projects, newest first.</param>
/// <param name="Refused">How many files were left out; opening one by id reports why.</param>
public sealed record ProjectListing(IReadOnlyList<ProjectSummary> Projects, int Refused);
