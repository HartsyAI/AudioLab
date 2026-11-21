namespace AudioLab.Conventions.Tests;

/// <summary>The only JavaScript and TypeScript the tests allow. DESIGN.md, "JavaScript policy", is the rule.</summary>
public static class ScriptAllowlist
{
    /// <summary>Approved script files in the repository. Adding one needs the owner's approval in the pull request that adds the file.</summary>
    public static readonly ScriptAllowance[] Entries = [];

    /// <summary>Approved NuGet packages that ship scripts, by package id. Adding one needs the owner's approval in its pull request.</summary>
    /// <remarks>An id covers every script the package ships, at any version.</remarks>
    public static readonly PackageScriptAllowance[] Packages =
    [
        new("Microsoft.AspNetCore.Components.WebAssembly", "The Blazor WebAssembly runtime loader (blazor.webassembly.js); the framework."),
        new("Microsoft.AspNetCore.App.Internal.Assets", "Ships the Blazor runtime loaders (blazor.*.js); only blazor.webassembly.js is served."),
        new("MudBlazor", "Dialog, menu and popover behaviour for the fallback proof (MudBlazor.min.js). Evaluated in O10."),
        new("Bit.Bheadless", "Focus trap, outside-click, positioning and live-region behaviour for menus and combo boxes. Evaluated in O10."),
    ];
}
