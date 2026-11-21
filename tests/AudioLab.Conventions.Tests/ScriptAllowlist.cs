namespace AudioLab.Conventions.Tests;

/// <summary>The only JavaScript and TypeScript the tests allow. DESIGN.md, "JavaScript policy", is the rule.</summary>
public static class ScriptAllowlist
{
    /// <summary>Approved script files in the repository. Adding one needs the owner's approval in the pull request that adds the file.</summary>
    public static readonly ScriptAllowance[] Entries = [];

    /// <summary>Approved NuGet packages that ship scripts, by package id. Adding one needs the owner's approval in its pull request.</summary>
    /// <remarks>An id covers every script the package ships, at any version.</remarks>
    public static readonly PackageScriptAllowance[] Packages = [];
}
