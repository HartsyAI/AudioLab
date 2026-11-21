namespace AudioLab.Conventions.Tests;

/// <summary>One approved NuGet package that ships script files: its package id, and why C# could not do the job.</summary>
public sealed record PackageScriptAllowance(string PackageId, string Reason);
