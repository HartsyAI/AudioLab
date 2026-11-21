namespace AudioLab.Conventions.Tests;

/// <summary>The only JavaScript and TypeScript the tests allow. DESIGN.md, "JavaScript policy", is the rule.</summary>
public static class ScriptAllowlist
{
    /// <summary>Approved script files in the repository. Adding one needs the owner's approval in the pull request that adds the file.</summary>
    public static readonly ScriptAllowance[] Entries = [];

<<<<<<< HEAD
    /// <summary>Approved NuGet packages that ship script files, by package id. Adding one needs the owner's approval in the pull request that references the package.</summary>
    public static readonly PackageScriptAllowance[] Packages =
    [
        new("Microsoft.AspNetCore.Components.WebAssembly", "The Blazor WebAssembly runtime loader (blazor.webassembly.js); the framework, not our code."),
        new("Microsoft.AspNetCore.App.Internal.Assets", "Ships the Blazor runtime loaders (blazor.*.js) with the ASP.NET Core targeting assets; only blazor.webassembly.js is served."),
        new("Bit.Bheadless", "Focus trap, outside-click, positioning and live-region behaviour for dialogs, menus, popovers and combo boxes; the browser has no C# binding for it. Evaluated in O10."),
    ];
=======
    /// <summary>Approved NuGet packages that ship scripts, by package id. Adding one needs the owner's approval in its pull request.</summary>
    /// <remarks>An id covers every script the package ships, at any version.</remarks>
    public static readonly PackageScriptAllowance[] Packages = [];
>>>>>>> hartsy/o10-script-allowlist-packages
}
