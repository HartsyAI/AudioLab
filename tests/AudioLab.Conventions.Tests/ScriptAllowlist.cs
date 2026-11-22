namespace AudioLab.Conventions.Tests;

/// <summary>The only JavaScript and TypeScript the tests allow. DESIGN.md, "JavaScript policy", is the rule.</summary>
public static class ScriptAllowlist
{
    private const string BlazorStart = "Starts the .NET runtime in the page, which only a script can do; the one file the app serves.";

    private const string BlazorUnused = "Shipped by the package beside blazor.webassembly.js; this app never serves or references it.";

    private const string BitReason = "D14 (Proposed): keyboard, focus and positioning of menus, select, popover, tooltip and toasts.";

    /// <summary>Approved script files in the repository. Adding one needs the owner's approval in the pull request that adds the file.</summary>
    public static readonly ScriptAllowance[] Entries = [];

    /// <summary>Approved scripts inside NuGet packages, one entry per script. Adding one needs the owner's approval in its pull request.</summary>
    /// <remarks>An entry covers one script path of one package id at one exact version. A new script, or a new version, needs a new entry.</remarks>
    public static readonly PackageScriptAllowance[] Packages =
    [
        new("Microsoft.AspNetCore.App.Internal.Assets", "10.0.14", "_framework/blazor.webassembly.js", BlazorStart),
        new("Microsoft.AspNetCore.App.Internal.Assets", "10.0.14", "_framework/blazor.server.js", BlazorUnused),
        new("Microsoft.AspNetCore.App.Internal.Assets", "10.0.14", "_framework/blazor.web.js", BlazorUnused),
        new("Microsoft.AspNetCore.Components.WebAssembly", "10.0.14", "build/net10.0/blazor.webassembly.js", BlazorUnused),
        new("Bit.Bheadless", "11.0.0-pre-23", "staticwebassets/Bit.Bheadless.4mg12f5a83.lib.module.js", BitReason),
        new("Bit.Bheadless", "11.0.0-pre-23", "staticwebassets/bit-bheadless-csp.js", BitReason),
        new("Bit.Bheadless", "11.0.0-pre-23", "staticwebassets/bit-bheadless.js", BitReason),
    ];
}
