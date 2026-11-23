namespace AudioLab.Conventions.Tests;

/// <summary>The only JavaScript and TypeScript the tests allow. DESIGN.md, "JavaScript policy", is the rule.</summary>
public static class ScriptAllowlist
{
    private const string Pack = "Microsoft.AspNetCore.App.Internal.Assets";

    /// <summary>Approved script files in the repository. Adding one needs the owner's approval in the pull request that adds the file.</summary>
    public static readonly ScriptAllowance[] Entries = [];

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
    ];
}
