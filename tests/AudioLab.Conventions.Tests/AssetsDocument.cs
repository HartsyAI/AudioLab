namespace AudioLab.Conventions.Tests;

/// <summary>The part of a project's obj/project.assets.json that lists restored libraries, keyed "id/version".</summary>
internal sealed record AssetsDocument(int Version, Dictionary<string, AssetsLibrary?>? Libraries);

/// <summary>One restored library: its kind ("package" or "project") and, for a package, every file it contains.</summary>
internal sealed record AssetsLibrary(string? Type, List<string>? Files);
