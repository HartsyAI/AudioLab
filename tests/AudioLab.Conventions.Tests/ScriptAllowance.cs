namespace AudioLab.Conventions.Tests;

/// <summary>One approved script file: its repository-relative path, and why C# could not do the job.</summary>
public sealed record ScriptAllowance(string Path, string Reason);
