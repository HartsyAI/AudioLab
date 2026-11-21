namespace AudioLab.Conventions.Tests;

/// <summary>A NuGet package that projects restored, with the path of every file it contains and the projects that restored it.</summary>
/// <remarks>Record equality compares the lists by reference, so tests assert on members.</remarks>
public sealed record PackageListing(string Id, string Version, IReadOnlyList<string> Files, IReadOnlyList<string> Projects);
