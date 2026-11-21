using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace AudioLab.Conventions.Tests;

/// <summary>Holds the repository to the JavaScript policy in DESIGN.md: C# first, script only by listed exception.</summary>
public sealed partial class ScriptPolicyTests
{
    private const string InteropFolder = "src/AudioLab.Web/Interop/";

    private static readonly Lazy<List<PackageListing>> _restored = new(RestoredPackages.Read);

    private static readonly string[] _markupExtensions = [".razor", ".cshtml", ".html", ".htm"];

    private static readonly string[] _interopIdentifiers = ["IJSRuntime", "IJSInProcessRuntime", "IJSObjectReference", "IJSInProcessObjectReference", "JSImport", "JSExport", "JSHost"];

    [Fact]
    public void EveryScriptFileIsOnTheAllowlist()
    {
        HashSet<string> allowed = [.. ScriptAllowlist.Entries.Select(entry => entry.Path)];
        List<string> files = [.. RepoFiles.WithExtensions(ScriptFiles.Extensions).Select(RepoFiles.Relative)];
        List<string> unlisted = [.. files.Where(path => !allowed.Contains(path))];
        Assert.True(unlisted.Count == 0, $"Script files that are not in ScriptAllowlist (write it in C#, or get the file approved): {string.Join(", ", unlisted)}");
    }

    [Fact]
    public void AllowlistEntriesExistAndGiveAReason()
    {
        foreach (ScriptAllowance entry in ScriptAllowlist.Entries)
        {
            Assert.True(File.Exists(Path.Combine(RepoFiles.Root, entry.Path)), $"ScriptAllowlist lists a file that does not exist: {entry.Path}");
            Assert.False(string.IsNullOrWhiteSpace(entry.Reason), $"ScriptAllowlist entry has no reason: {entry.Path}");
        }
    }

    [Fact]
    public void EveryPackageScriptIsOnTheAllowlist()
    {
        List<string> unlisted = PackageScriptRules.Unlisted(_restored.Value, ScriptAllowlist.Packages);
        Assert.True(
            unlisted.Count == 0,
            $"Restored packages that ship scripts and are not in ScriptAllowlist.Packages: {string.Join("; ", unlisted)}");
    }

    [Fact]
    public void PackageAllowlistEntriesAreRestoredAndGiveAReason()
    {
        List<string> problems = PackageScriptRules.EntryProblems(_restored.Value, ScriptAllowlist.Packages);
        Assert.True(problems.Count == 0, $"ScriptAllowlist.Packages has entries that do not hold: {string.Join("; ", problems)}");
    }

    [Fact]
    public void MarkupHasNoInlineScript()
    {
        List<string> offenders = [.. RepoFiles.WithExtensions(_markupExtensions).Where(file => InlineScript().IsMatch(File.ReadAllText(file))).Select(RepoFiles.Relative)];
        Assert.True(offenders.Count == 0, $"Markup with an inline <script> block (move it to an allowlisted file, or to C#): {string.Join(", ", offenders)}");
    }

    [Fact]
    public void JavaScriptInteropStaysInTheInteropFolder()
    {
        List<string> offenders = [];
        foreach (string file in RepoFiles.WithExtensions(".cs"))
        {
            string relative = RepoFiles.Relative(file);
            if (!relative.StartsWith(InteropFolder, StringComparison.Ordinal) && UsesInterop(file))
            {
                offenders.Add(relative);
            }
        }
        Assert.True(offenders.Count == 0, $"JavaScript interop outside {InteropFolder} (call the C# interface that folder exposes instead): {string.Join(", ", offenders)}");
    }

    private static bool UsesInterop(string file)
    {
        SyntaxNode root = CSharpSyntaxTree.ParseText(File.ReadAllText(file)).GetRoot();
        return root.DescendantTokens().Any(token => token.IsKind(SyntaxKind.IdentifierToken) && _interopIdentifiers.Contains(token.Text));
    }

    [GeneratedRegex(@"<script(?![^>]*\bsrc\s*=)[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex InlineScript();
}
