namespace AudioLab.Conventions.Tests;

/// <summary>Holds the package half of the script policy to its own contract, on synthetic package listings.</summary>
public sealed class PackageScriptRuleTests
{
    private static readonly PackageScriptAllowance[] _none = [];

    private static readonly PackageListing _widgets = Package("Acme.Widgets", "1.2.0", "lib/net10.0/Acme.Widgets.dll", "staticwebassets/widgets.js");

    private static readonly PackageScriptAllowance _widgetsScript = new("Acme.Widgets", "1.2.0", "staticwebassets/widgets.js", "Focus handling.");

    private static readonly PackageListing _plain =
        Package("Acme.Plain", "3.0.0", "lib/net10.0/Acme.Plain.dll", "staticwebassets/plain.css", "README.md");

    [Theory]
    [InlineData("staticwebassets/app.js", true)]
    [InlineData("staticwebassets/app.mjs", true)]
    [InlineData("build/loader.cjs", true)]
    [InlineData("content/app.jsx", true)]
    [InlineData("content/app.ts", true)]
    [InlineData("content/app.mts", true)]
    [InlineData("content/app.cts", true)]
    [InlineData("content/app.tsx", true)]
    [InlineData("content/types.d.ts", true)]
    [InlineData("content/App.JS", true)]
    [InlineData("staticwebassets\\app.js", true)]
    [InlineData("staticwebassets/app.js.map", false)]
    [InlineData("staticwebassets/app.css", false)]
    [InlineData("staticwebassets/index.html", false)]
    [InlineData("lib/net10.0/Acme.dll", false)]
    [InlineData("content/javascript/notes.txt", false)]
    [InlineData("content/app.js/README", false)]
    [InlineData("content/js", false)]
    public void ScriptFilesAreRecognisedByExtensionWithEitherSlash(string path, bool expected)
    {
        Assert.Equal(expected, ScriptFiles.IsScript(path));
    }

    [Fact]
    public void ListedScriptOfThePinnedVersionIsAccepted()
    {
        Assert.Empty(PackageScriptRules.Unlisted([_widgets], [_widgetsScript]));
    }

    [Fact]
    public void ListedIdIgnoresCaseButNotThePath()
    {
        Assert.Empty(PackageScriptRules.Unlisted([_widgets], [_widgetsScript with { PackageId = "acme.widgets" }]));
        Assert.Single(PackageScriptRules.Unlisted([_widgets], [_widgetsScript with { Script = "staticwebassets/WIDGETS.js" }]));
    }

    [Fact]
    public void ListedScriptWrittenWithBackslashesIsAccepted()
    {
        Assert.Empty(PackageScriptRules.Unlisted([_widgets], [_widgetsScript with { Script = "staticwebassets\\widgets.js" }]));
    }

    [Fact]
    public void UnlistedScriptInAnApprovedPackageIsRejected()
    {
        PackageListing package = Package("Acme.Widgets", "1.2.0", "staticwebassets/widgets.js", "staticwebassets/extra.js");
        string line = Assert.Single(PackageScriptRules.Unlisted([package], [_widgetsScript]));
        Assert.Contains("1 script file(s) that are not approved: staticwebassets/extra.js", line, StringComparison.Ordinal);
        Assert.DoesNotContain("not approved: staticwebassets/widgets.js", line, StringComparison.Ordinal);
        Assert.Contains("new(\"Acme.Widgets\", \"1.2.0\", \"staticwebassets/extra.js\", ", line, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("1.2.1")]
    [InlineData("1.3.0")]
    [InlineData("2.0.0")]
    [InlineData("1.2.0-beta")]
    [InlineData("1.2")]
    public void ApprovedScriptDoesNotCarryOverToAnotherVersion(string version)
    {
        PackageListing bumped = Package("Acme.Widgets", version, "staticwebassets/widgets.js");
        string line = Assert.Single(PackageScriptRules.Unlisted([bumped], [_widgetsScript]));
        Assert.StartsWith($"Acme.Widgets {version} ", line, StringComparison.Ordinal);
        Assert.Contains("entries for it at 1.2.0", line, StringComparison.Ordinal);
    }

    [Fact]
    public void EachRestoredVersionNeedsItsOwnEntry()
    {
        PackageListing newer = Package("Acme.Widgets", "1.3.0", "staticwebassets/widgets.js");
        PackageScriptAllowance[] both = [_widgetsScript, _widgetsScript with { Version = "1.3.0" }];
        Assert.Empty(PackageScriptRules.Unlisted([_widgets, newer], both));
        string line = Assert.Single(PackageScriptRules.Unlisted([_widgets, newer], [_widgetsScript]));
        Assert.StartsWith("Acme.Widgets 1.3.0 ", line, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Acme.Widgets.Extras")]
    [InlineData("Acme.Widget")]
    [InlineData(" Acme.Widgets")]
    [InlineData("Acme.Widgets ")]
    public void ListedEntryCoversOnlyThatExactId(string other)
    {
        PackageListing neighbour = Package(other, "1.2.0", "staticwebassets/widgets.js");
        string line = Assert.Single(PackageScriptRules.Unlisted([_widgets, neighbour], [_widgetsScript]));
        Assert.StartsWith(other, line, StringComparison.Ordinal);
    }

    [Fact]
    public void PackageWithoutScriptsNeedsNoEntry()
    {
        Assert.Empty(PackageScriptRules.Unlisted([_plain], _none));
    }

    [Theory]
    [InlineData("staticwebassets/app.js", "staticwebassets/app.js")]
    [InlineData("staticwebassets/app.mjs", "staticwebassets/app.mjs")]
    [InlineData("build/net10.0/loader.cjs", "build/net10.0/loader.cjs")]
    [InlineData("content/Scripts/app.ts", "content/Scripts/app.ts")]
    [InlineData("content/Scripts/app.mts", "content/Scripts/app.mts")]
    [InlineData("content/Scripts/types.d.ts", "content/Scripts/types.d.ts")]
    [InlineData("content/Scripts/App.JS", "content/Scripts/App.JS")]
    [InlineData("staticwebassets\\app.js", "staticwebassets/app.js")]
    public void UnlistedPackageShippingAScriptIsRejected(string script, string shown)
    {
        PackageListing package = Package("Acme.Widgets", "1.2.0", "lib/net10.0/Acme.Widgets.dll", script);
        string line = Assert.Single(PackageScriptRules.Unlisted([package], _none));
        Assert.Contains("Acme.Widgets", line, StringComparison.Ordinal);
        Assert.Contains(shown, line, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectionNamesEveryScriptTheProjectsAndACopyReadyEntryPerScript()
    {
        PackageListing package = new(
            "Acme.Widgets",
            "1.2.0",
            ["a/one.js", "b/two.mjs", "lib/Acme.dll"],
            ["src/Web/Web.csproj", "src/Other/Other.csproj"]);
        string line = Assert.Single(PackageScriptRules.Unlisted([package], _none));
        Assert.Contains("2 script file(s)", line, StringComparison.Ordinal);
        Assert.Contains("a/one.js, b/two.mjs", line, StringComparison.Ordinal);
        Assert.DoesNotContain("Acme.dll", line, StringComparison.Ordinal);
        Assert.Contains("src/Web/Web.csproj, src/Other/Other.csproj", line, StringComparison.Ordinal);
        Assert.Contains("new(\"Acme.Widgets\", \"1.2.0\", \"a/one.js\", ", line, StringComparison.Ordinal);
        Assert.Contains("new(\"Acme.Widgets\", \"1.2.0\", \"b/two.mjs\", ", line, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("staticwebassets/app.js.map")]
    [InlineData("staticwebassets/app.css")]
    [InlineData("staticwebassets/index.html")]
    [InlineData("lib/net10.0/Acme.Widgets.dll")]
    [InlineData("content/javascript/notes.txt")]
    public void FilesThatAreNotScriptsAreIgnored(string file)
    {
        Assert.Empty(PackageScriptRules.Unlisted([Package("Acme.Widgets", "1.2.0", file)], _none));
    }

    [Fact]
    public void EntryForAShippedScriptHolds()
    {
        Assert.Empty(PackageScriptRules.EntryProblems([_widgets, _plain], [_widgetsScript]));
    }

    [Fact]
    public void EntryForAScriptTheVersionNoLongerShipsIsRejected()
    {
        PackageScriptAllowance[] stale = [_widgetsScript with { Script = "staticwebassets/gone.js" }];
        string line = Assert.Single(PackageScriptRules.EntryProblems([_widgets], stale));
        Assert.Contains("does not ship that script file", line, StringComparison.Ordinal);
    }

    [Fact]
    public void EntryForAnOldVersionIsRejectedAfterABump()
    {
        PackageListing bumped = Package("Acme.Widgets", "1.3.0", "staticwebassets/widgets.js");
        string line = Assert.Single(PackageScriptRules.EntryProblems([bumped], [_widgetsScript]));
        Assert.Contains("Acme.Widgets 1.2.0 is not restored by any project (restored: 1.3.0)", line, StringComparison.Ordinal);
    }

    [Fact]
    public void EntriesHoldWhenTwoVersionsAreRestoredAndEachIsListed()
    {
        PackageListing newer = Package("Acme.Widgets", "1.3.0", "staticwebassets/widgets.js");
        PackageScriptAllowance[] both = [_widgetsScript, _widgetsScript with { Version = "1.3.0" }];
        Assert.Empty(PackageScriptRules.EntryProblems([_widgets, newer], both));
    }

    [Theory]
    [InlineData("", "1.2.0", "staticwebassets/widgets.js", "Focus handling.")]
    [InlineData("  ", "1.2.0", "staticwebassets/widgets.js", "Focus handling.")]
    [InlineData("Acme.Widgets", "", "staticwebassets/widgets.js", "Focus handling.")]
    [InlineData("Acme.Widgets", "1.2.0", " ", "Focus handling.")]
    [InlineData("Acme.Widgets", "1.2.0", "staticwebassets/widgets.js", "  ")]
    public void EntryWithABlankFieldIsRejected(string id, string version, string script, string reason)
    {
        string line = Assert.Single(PackageScriptRules.EntryProblems([_widgets], [new(id, version, script, reason)]));
        Assert.Contains("needs a package id, a version, a script path and a reason", line, StringComparison.Ordinal);
    }

    [Fact]
    public void EntryForAPackageNoProjectRestoresIsRejected()
    {
        PackageScriptAllowance[] gone = [_widgetsScript with { PackageId = "Acme.Gone" }];
        string line = Assert.Single(PackageScriptRules.EntryProblems([_widgets], gone));
        Assert.Contains("Acme.Gone 1.2.0 is not restored by any project (no version of it is restored)", line, StringComparison.Ordinal);
    }

    [Fact]
    public void PaddedEntryDoesNotMatchTheRestoredPackage()
    {
        string line = Assert.Single(PackageScriptRules.EntryProblems([_widgets], [_widgetsScript with { PackageId = " Acme.Widgets" }]));
        Assert.Contains("is not restored", line, StringComparison.Ordinal);
    }

    [Fact]
    public void EntryForAFileThatIsNotAScriptIsRejected()
    {
        PackageListing package = Package("Acme.Widgets", "1.2.0", "staticwebassets/widgets.css");
        PackageScriptAllowance[] css = [_widgetsScript with { Script = "staticwebassets/widgets.css" }];
        string line = Assert.Single(PackageScriptRules.EntryProblems([package], css));
        Assert.Contains("does not ship that script file", line, StringComparison.Ordinal);
    }

    [Fact]
    public void RepeatedEntryIsRejected()
    {
        PackageScriptAllowance[] twice = [_widgetsScript, _widgetsScript with { PackageId = "ACME.WIDGETS", Reason = "Again." }];
        string line = Assert.Single(PackageScriptRules.EntryProblems([_widgets], twice));
        Assert.Contains("ACME.WIDGETS 1.2.0 staticwebassets/widgets.js is listed twice", line, StringComparison.Ordinal);
    }

    [Fact]
    public void RepeatedEntryWithBackslashesIsRejected()
    {
        PackageScriptAllowance[] twice = [_widgetsScript, _widgetsScript with { Script = "staticwebassets\\widgets.js" }];
        Assert.Contains("listed twice", Assert.Single(PackageScriptRules.EntryProblems([_widgets], twice)), StringComparison.Ordinal);
    }

    private static PackageListing Package(string id, string version, params string[] files)
    {
        return new PackageListing(id, version, files, ["src/Web/Web.csproj"]);
    }
}
