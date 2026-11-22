namespace AudioLab.Conventions.Tests;

/// <summary>The only JavaScript and TypeScript the tests allow. DESIGN.md, "JavaScript policy", is the rule.</summary>
public static class ScriptAllowlist
{
    /// <summary>Approved script files in the repository. Adding one needs the owner's approval in the pull request that adds the file.</summary>
    public static readonly ScriptAllowance[] Entries =
    [
        new(
            "src/AudioLab.Web/wwwroot/audio-shim.js",
            "O1 spike, not for merge. An AudioWorkletProcessor must be a script (it runs on the audio thread, where .NET does not), and a web worker needs a script to start the .NET runtime; one file holds both roles and only moves rendered chunks."),
    ];

    /// <summary>Approved scripts inside NuGet packages, one entry per script. Adding one needs the owner's approval in its pull request.</summary>
    /// <remarks>An entry covers one script path of one package id at one exact version. A new script, or a new version, needs a new entry.</remarks>
    public static readonly PackageScriptAllowance[] Packages =
    [
        new("Microsoft.AspNetCore.App.Internal.Assets", "10.0.14", "_framework/blazor.server.js", "O1 spike, not for merge: the Blazor runtime ships it; the page does not load it."),
        new("Microsoft.AspNetCore.App.Internal.Assets", "10.0.14", "_framework/blazor.web.js", "O1 spike, not for merge: the Blazor runtime ships it; the page does not load it."),
        new("Microsoft.AspNetCore.App.Internal.Assets", "10.0.14", "_framework/blazor.webassembly.js", "O1 spike, not for merge: the Blazor WebAssembly loader, the same one every Blazor WebAssembly app serves."),
        new("Microsoft.AspNetCore.Components.WebAssembly", "10.0.14", "build/net10.0/blazor.webassembly.js", "O1 spike, not for merge: the Blazor WebAssembly loader, the same one every Blazor WebAssembly app serves."),
    ];
}
