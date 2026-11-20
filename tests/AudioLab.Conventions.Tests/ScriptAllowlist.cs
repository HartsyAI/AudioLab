namespace AudioLab.Conventions.Tests;

/// <summary>The only JavaScript and TypeScript files the repository may contain. DESIGN.md, "JavaScript policy", is the rule.</summary>
public static class ScriptAllowlist
{
    /// <summary>Approved script files. Adding one needs the owner's approval in the pull request that adds the file.</summary>
    public static readonly ScriptAllowance[] Entries = [];
}
