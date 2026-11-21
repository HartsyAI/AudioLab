namespace AudioLab.Conventions.Tests;

/// <summary>One approved script in a NuGet package: id, exact version, path in the package, and why C# could not do the job.</summary>
public sealed record PackageScriptAllowance(string PackageId, string Version, string Script, string Reason);
