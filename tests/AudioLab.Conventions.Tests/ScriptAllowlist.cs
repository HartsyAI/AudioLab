namespace AudioLab.Conventions.Tests;

/// <summary>The only JavaScript and TypeScript the tests allow. DESIGN.md, "JavaScript policy", is the rule.</summary>
public static class ScriptAllowlist
{
    /// <summary>Approved script files in the repository. Adding one needs the owner's approval in the pull request that adds the file.</summary>
    public static readonly ScriptAllowance[] Entries = [];

    /// <summary>Approved scripts inside NuGet packages, one entry per script. Adding one needs the owner's approval in its pull request.</summary>
    /// <remarks>An entry covers one script path of one package id at one exact version. A new script, or a new version, needs a new entry.</remarks>
    public static readonly PackageScriptAllowance[] Packages = [];
}
