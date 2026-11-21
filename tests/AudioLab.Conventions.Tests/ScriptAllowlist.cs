namespace AudioLab.Conventions.Tests;

/// <summary>The only JavaScript and TypeScript the tests allow. DESIGN.md, "JavaScript policy", is the rule.</summary>
public static class ScriptAllowlist
{
    /// <summary>Approved script files in the repository. Adding one needs the owner's approval in the pull request that adds the file.</summary>
    public static readonly ScriptAllowance[] Entries = [];

    /// <summary>Approved scripts inside NuGet packages, one entry per script. Adding one needs the owner's approval in its pull request.</summary>
    /// <remarks>An entry covers one script path of one package id at one exact version. A new script, or a new version, needs a new entry.</remarks>
    public static readonly PackageScriptAllowance[] Packages =
    [
        new("Bit.Bheadless", "11.0.0-pre-23", "staticwebassets/Bit.Bheadless.4mg12f5a83.lib.module.js", "Spike only: Blazor loads it as the library's initialiser."),
        new("Bit.Bheadless", "11.0.0-pre-23", "staticwebassets/bit-bheadless-csp.js", "Spike only: the library's behaviour script, CSP build."),
        new("Bit.Bheadless", "11.0.0-pre-23", "staticwebassets/bit-bheadless.js", "Spike only: the library's behaviour script."),
        new("Microsoft.AspNetCore.App.Internal.Assets", "10.0.14", "_framework/blazor.server.js", "Spike only: Blazor runtime loader."),
        new("Microsoft.AspNetCore.App.Internal.Assets", "10.0.14", "_framework/blazor.web.js", "Spike only: Blazor runtime loader."),
        new("Microsoft.AspNetCore.App.Internal.Assets", "10.0.14", "_framework/blazor.webassembly.js", "Spike only: Blazor runtime loader."),
        new("Microsoft.AspNetCore.Components.WebAssembly", "10.0.14", "build/net10.0/blazor.webassembly.js", "Spike only: Blazor runtime loader."),
        new("MudBlazor", "9.14.0", "staticwebassets/MudBlazor.min.js", "Spike only: MudBlazor's behaviour script."),
    ];
}
