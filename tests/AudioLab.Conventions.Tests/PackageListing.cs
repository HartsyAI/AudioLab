namespace AudioLab.Conventions.Tests;

/// <summary>A NuGet package that a project restored, with the path of every file the package contains.</summary>
public sealed record PackageListing(string Id, string Version, IReadOnlyList<string> Files);
