namespace AudioLab.Conventions.Tests;

/// <summary>The only JavaScript and TypeScript the tests allow. DESIGN.md, "JavaScript policy", is the rule.</summary>
public static class ScriptAllowlist
{
    private const string Pack = "Microsoft.AspNetCore.App.Internal.Assets";

    private const string Bit = "Bit.Bheadless";

    /// <summary>Approved script files in the repository. Adding one needs the owner's approval in the pull request that adds the file.</summary>
    public static readonly ScriptAllowance[] Entries =
    [
        new("src/AudioLab.Web/wwwroot/audio-shim.js",
            "Two roles, no engine logic (D24). First, the AudioWorkletProcessor: a processor can only be a script, and it keeps the ring of "
            + "rendered chunks, the credits and the dropout counter on the audio thread. Second, the bootstrap of the engine's web worker: a "
            + "worker can only start from a script, and it must start the .NET runtime, call its exports and pass each chunk to the worklet's port."),
    ];

    /// <summary>Approved scripts inside NuGet packages, one entry per script. Adding one needs the owner's approval in its pull request.</summary>
    /// <remarks>An entry covers one script path of one package id at one exact version. A new script, or a new version, needs a new entry.</remarks>
    public static readonly PackageScriptAllowance[] Packages =
    [
        new(Pack, "10.0.14", "_framework/blazor.webassembly.js",
            "Starts the .NET runtime in the page, which only a script can do. index.html loads it and the server serves it; UiRouteTests fetch it."),
        new(Pack, "10.0.14", "_framework/blazor.server.js",
            "The circuit client of Blazor Server, in the same package. This app has no circuit; UiRouteTests check the server does not serve it."),
        new(Pack, "10.0.14", "_framework/blazor.web.js",
            "The client of a Blazor Web App, shipped in the same package. This app is not one; UiRouteTests check the server does not serve it."),
        new("Microsoft.AspNetCore.Components.WebAssembly", "10.0.14", "build/net10.0/blazor.webassembly.js",
            "A byte for byte copy of blazor.webassembly.js that the package keeps for its build targets. The app serves the pack's copy, not this."),
        new(Bit, "11.0.0-pre-23", "staticwebassets/Bit.Bheadless.4mg12f5a83.lib.module.js",
            "D14 (Decided). Blazor loads it by itself at start and installs the key handling and dismissable layers of the menu and tooltip."),
        new(Bit, "11.0.0-pre-23", "staticwebassets/bit-bheadless.js",
            "D14 (Decided). The behaviour the menu and tooltip call: focus, key handling and positioning. The file name carries no hash."),
        new(Bit, "11.0.0-pre-23", "staticwebassets/bit-bheadless-csp.js",
            "D14 (Decided). Applies the positions the library computes through the style object, for a page under a strict content policy."),
    ];
}
