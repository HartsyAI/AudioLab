namespace AudioLab.Conventions.Tests;

/// <summary>Holds the package half of the script policy to its own contract, on synthetic package listings.</summary>
public sealed class PackageScriptRuleTests
{
    private static readonly PackageScriptAllowance[] _none = [];

    private static readonly PackageListing _widgets = Package("Acme.Widgets", "1.2.0", "lib/net10.0/Acme.Widgets.dll", "staticwebassets/widgets.js");

    private static readonly PackageListing _plain = Package("Acme.Plain", "3.0.0", "lib/net10.0/Acme.Plain.dll", "staticwebassets/plain.css", "README.md");

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
    [InlineData("content/Scripts/App.JS")]
    public void UnlistedPackageShippingAScriptIsRejected(string script)
    {
        List<string> unlisted = PackageScriptRules.Unlisted([Package("Acme.Widgets", "1.2.0", "lib/net10.0/Acme.Widgets.dll", script)], _none);
        string line = Assert.Single(unlisted);
        Assert.Contains("Acme.Widgets", line, StringComparison.Ordinal);
        Assert.Contains(script, line, StringComparison.Ordinal);
    }

    [Fact]
    public void ListingOnePackageDoesNotCoverAnother()
    {
        PackageListing other = Package("Acme.Other", "2.0.0", "staticwebassets/other.js");
        PackageScriptAllowance[] allowed = [new("Acme.Widgets", "Focus handling.")];
        string line = Assert.Single(PackageScriptRules.Unlisted([_widgets, other, _plain], allowed));
        Assert.StartsWith("Acme.Other", line, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("staticwebassets/app.js.map")]
    [InlineData("staticwebassets/app.css")]
    [InlineData("lib/net10.0/Acme.Widgets.dll")]
    [InlineData("content/javascript/notes.txt")]
    public void FilesThatAreNotScriptsAreIgnored(string file)
    {
        Assert.Empty(PackageScriptRules.Unlisted([Package("Acme.Widgets", "1.2.0", file)], _none));
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
        Assert.Single(PackageScriptRules.EntryProblems([_widgets], allowed));
    }

    [Fact]
    public void EntryForAPackageNoProjectRestoresIsRejected()
    {
        PackageScriptAllowance[] allowed = [new("Acme.Gone", "Focus handling.")];
        string line = Assert.Single(PackageScriptRules.EntryProblems([_widgets], allowed));
        Assert.Contains("not restored", line, StringComparison.Ordinal);
    }

    [Fact]
    public void EntryForAPackageThatShipsNoScriptIsRejected()
    {
        PackageScriptAllowance[] allowed = [new("Acme.Plain", "Focus handling.")];
        string line = Assert.Single(PackageScriptRules.EntryProblems([_plain], allowed));
        Assert.Contains("ships no script", line, StringComparison.Ordinal);
    }

    [Fact]
    public void RepeatedEntryIsRejected()
    {
        PackageScriptAllowance[] allowed = [new("Acme.Widgets", "Focus handling."), new("ACME.WIDGETS", "Again.")];
        string line = Assert.Single(PackageScriptRules.EntryProblems([_widgets], allowed));
        Assert.Contains("listed twice", line, StringComparison.Ordinal);
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
        return new PackageListing(id, version, files);
    }
}
