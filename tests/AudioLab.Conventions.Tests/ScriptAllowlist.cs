namespace AudioLab.Conventions.Tests;

/// <summary>The only JavaScript and TypeScript the repository may contain. DESIGN.md, "JavaScript policy", is the rule.</summary>
public static class ScriptAllowlist
{
    /// <summary>Approved script files in the repository. Adding one needs the owner's approval in the pull request that adds the file.</summary>
    public static readonly ScriptAllowance[] Entries = [];

    /// <summary>Approved NuGet packages that ship script files, by package id. Adding one needs the owner's approval in the pull request that references the package.</summary>
    public static readonly PackageScriptAllowance[] Packages = [];
}
