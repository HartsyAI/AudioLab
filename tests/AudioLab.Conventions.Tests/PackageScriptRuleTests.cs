namespace AudioLab.Conventions.Tests;

/// <summary>Holds the package half of the script policy to its own contract, on synthetic package listings.</summary>
public sealed class PackageScriptRuleTests
{
    private static readonly PackageScriptAllowance[] _none = [];

    private static readonly PackageListing _widgets = Package("Acme.Widgets", "1.2.0", "lib/net10.0/Acme.Widgets.dll", "staticwebassets/widgets.js");

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
    public void PackageWithAListedIdIsAccepted()
    {
        PackageScriptAllowance[] allowed = [new("Acme.Widgets", "Focus handling the browser only offers through script.")];
        Assert.Empty(PackageScriptRules.Unlisted([_widgets], allowed));
    }

    [Fact]
    public void ListedIdIgnoresCase()
    {
        PackageScriptAllowance[] allowed = [new("acme.widgets", "Focus handling.")];
        Assert.Empty(PackageScriptRules.Unlisted([_widgets], allowed));
    }

    [Theory]
    [InlineData("Acme.Widgets.Extras")]
    [InlineData("Acme.Widget")]
    [InlineData(" Acme.Widgets")]
    [InlineData("Acme.Widgets ")]
    public void ListedIdCoversOnlyThatExactId(string other)
    {
        PackageListing neighbour = Package(other, "2.0.0", "staticwebassets/other.js");
        PackageScriptAllowance[] allowed = [new("Acme.Widgets", "Focus handling.")];
        string line = Assert.Single(PackageScriptRules.Unlisted([_widgets, neighbour], allowed));
        Assert.StartsWith(other, line, StringComparison.Ordinal);
    }

    [Fact]
    public void PackageWithoutScriptsNeedsNoEntry()
    {
        Assert.Empty(PackageScriptRules.Unlisted([_plain], _none));
    }

    [Theory]
    [InlineData("staticwebassets/app.js")]
    [InlineData("staticwebassets/app.mjs")]
    [InlineData("build/net10.0/loader.cjs")]
    [InlineData("content/Scripts/app.ts")]
    [InlineData("content/Scripts/app.mts")]
    [InlineData("content/Scripts/types.d.ts")]
    [InlineData("content/Scripts/App.JS")]
    [InlineData("staticwebassets\\app.js")]
    public void UnlistedPackageShippingAScriptIsRejected(string script)
    {
        List<string> unlisted = PackageScriptRules.Unlisted([Package("Acme.Widgets", "1.2.0", "lib/net10.0/Acme.Widgets.dll", script)], _none);
        string line = Assert.Single(unlisted);
        Assert.Contains("Acme.Widgets", line, StringComparison.Ordinal);
        Assert.Contains(script, line, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectionNamesEveryScriptTheProjectsAndACopyReadyEntry()
    {
        PackageListing package = new(
            "Acme.Widgets",
            "1.2.0",
            ["a/one.js", "b/two.mjs", "lib/Acme.dll"],
            ["src/Web/Web.csproj", "src/Other/Other.csproj"]);
        string line = Assert.Single(PackageScriptRules.Unlisted([package], _none));
        Assert.Contains("2 script file(s)", line, StringComparison.Ordinal);
        Assert.Contains("a/one.js", line, StringComparison.Ordinal);
        Assert.Contains("b/two.mjs", line, StringComparison.Ordinal);
        Assert.DoesNotContain("Acme.dll", line, StringComparison.Ordinal);
        Assert.Contains("src/Web/Web.csproj, src/Other/Other.csproj", line, StringComparison.Ordinal);
        Assert.Contains("new(\"Acme.Widgets\", ", line, StringComparison.Ordinal);
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
    public void EveryRestoredVersionIsCheckedAgainstTheListedId()
    {
        PackageListing older = Package("Acme.Widgets", "1.0.0", "lib/net10.0/Acme.Widgets.dll");
        string line = Assert.Single(PackageScriptRules.Unlisted([older, _widgets], _none));
        Assert.Contains("Acme.Widgets 1.2.0", line, StringComparison.Ordinal);
    }

    [Fact]
    public void EntryForARestoredScriptPackageHolds()
    {
        PackageScriptAllowance[] allowed = [new("Acme.Widgets", "Focus handling.")];
        Assert.Empty(PackageScriptRules.EntryProblems([_widgets, _plain], allowed));
    }

    [Fact]
    public void EntryWithoutAReasonIsRejected()
    {
        PackageScriptAllowance[] allowed = [new("Acme.Widgets", "  ")];
        string line = Assert.Single(PackageScriptRules.EntryProblems([_widgets], allowed));
        Assert.Contains("'Acme.Widgets' needs a package id and a reason", line, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void EntryWithoutAnIdIsRejected(string id)
    {
        PackageScriptAllowance[] allowed = [new(id, "Focus handling.")];
        string line = Assert.Single(PackageScriptRules.EntryProblems([_widgets], allowed));
        Assert.Contains("needs a package id and a reason", line, StringComparison.Ordinal);
    }

    [Fact]
    public void EntryForAPackageNoProjectRestoresIsRejected()
    {
        PackageScriptAllowance[] allowed = [new("Acme.Gone", "Focus handling.")];
        string line = Assert.Single(PackageScriptRules.EntryProblems([_widgets], allowed));
        Assert.Contains("Acme.Gone is not restored", line, StringComparison.Ordinal);
    }

    [Fact]
    public void PaddedEntryDoesNotMatchTheRestoredPackage()
    {
        PackageScriptAllowance[] allowed = [new(" Acme.Widgets", "Focus handling.")];
        string line = Assert.Single(PackageScriptRules.EntryProblems([_widgets], allowed));
        Assert.Contains("is not restored", line, StringComparison.Ordinal);
    }

    [Fact]
    public void EntryForAPackageThatShipsNoScriptIsRejected()
    {
        PackageScriptAllowance[] allowed = [new("Acme.Plain", "Focus handling.")];
        string line = Assert.Single(PackageScriptRules.EntryProblems([_plain], allowed));
        Assert.Contains("Acme.Plain ships no script", line, StringComparison.Ordinal);
    }

    [Fact]
    public void RepeatedEntryIsRejected()
    {
        PackageScriptAllowance[] allowed = [new("Acme.Widgets", "Focus handling."), new("ACME.WIDGETS", "Again.")];
        string line = Assert.Single(PackageScriptRules.EntryProblems([_widgets], allowed));
        Assert.Contains("ACME.WIDGETS is listed twice", line, StringComparison.Ordinal);
    }

    [Fact]
    public void EntryHoldsWhenOnlyOneRestoredVersionShipsAScript()
    {
        PackageListing older = Package("Acme.Widgets", "1.0.0", "lib/net10.0/Acme.Widgets.dll");
        PackageScriptAllowance[] allowed = [new("Acme.Widgets", "Focus handling.")];
        Assert.Empty(PackageScriptRules.EntryProblems([older, _widgets], allowed));
    }

    private static PackageListing Package(string id, string version, params string[] files)
    {
        return new PackageListing(id, version, files, ["src/Web/Web.csproj"]);
    }
}
