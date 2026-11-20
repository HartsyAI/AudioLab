using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AudioLab.Conventions.Tests;

/// <summary>Checks the code-style rules in AGENTS.md that no compiler analyzer covers.</summary>
public sealed partial class CodeStyleTests
{
    [Fact]
    public void NoDynamicTypes()
    {
        List<string> offenders = [.. SourceFiles().Where(file => Parse(file).DescendantNodes().OfType<IdentifierNameSyntax>().Any(name => name.Identifier.Text == "dynamic")).Select(RepoFiles.Relative)];
        Assert.True(offenders.Count == 0, $"Files that use 'dynamic' (use an explicit type): {string.Join(", ", offenders)}");
    }

    [Fact]
    public void OnePublicTypePerFile()
    {
        List<string> offenders = [.. SourceFiles().Where(file => PublicTopLevelTypes(Parse(file)) > 1).Select(RepoFiles.Relative)];
        Assert.True(offenders.Count == 0, $"Files that declare more than one public top-level type: {string.Join(", ", offenders)}");
    }

    [Fact]
    public void SummaryTagsShareALineWithTheirText()
    {
        List<string> offenders = [.. SourceFiles().Where(file => File.ReadLines(file).Any(line => LoneSummaryTag().IsMatch(line))).Select(RepoFiles.Relative)];
        Assert.True(offenders.Count == 0, $"Files with <summary> or </summary> alone on a line (keep the tags on the line of the text): {string.Join(", ", offenders)}");
    }

    [Fact]
    public void RazorFilesHoldMarkupOnly()
    {
        List<string> offenders = [.. RepoFiles.WithExtensions(".razor").Where(file => File.ReadLines(file).Any(line => CodeBlock().IsMatch(line))).Select(RepoFiles.Relative)];
        Assert.True(offenders.Count == 0, $"Razor files with an @code or @functions block (move it to the .razor.cs partial class): {string.Join(", ", offenders)}");
    }

    private static List<string> SourceFiles()
    {
        return RepoFiles.WithExtensions(".cs");
    }

    private static CompilationUnitSyntax Parse(string file)
    {
        CSharpParseOptions options = new(LanguageVersion.Preview);
        return CSharpSyntaxTree.ParseText(File.ReadAllText(file), options).GetCompilationUnitRoot();
    }

    private static int PublicTopLevelTypes(CompilationUnitSyntax root)
    {
        IEnumerable<SyntaxNode> topLevel = root.DescendantNodes(node => node is CompilationUnitSyntax or BaseNamespaceDeclarationSyntax);
        return topLevel.OfType<MemberDeclarationSyntax>().Count(member => member is BaseTypeDeclarationSyntax or DelegateDeclarationSyntax && member.Modifiers.Any(SyntaxKind.PublicKeyword));
    }

    [GeneratedRegex(@"^\s*///\s*</?summary>\s*$")]
    private static partial Regex LoneSummaryTag();

    [GeneratedRegex(@"^\s*@(code|functions)\b")]
    private static partial Regex CodeBlock();
}
