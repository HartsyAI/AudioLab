using System.Globalization;
using System.Text.RegularExpressions;

namespace AudioLab.Conventions.Tests;

/// <summary>Holds every style in the repository, and the session mock, to the design system in DESIGN.md section 7.</summary>
public sealed partial class DesignTokenTests
{
    private const double TextContrastFloor = 4.5;

    private static readonly string[] _fillTokens = ["--accent", "--record", "--go", "--caution", "--mute"];

    private static readonly string[] _tokenOnlyProperties = ["font", "font-size", "font-family", "box-shadow", "z-index"];

    private static readonly string[] _keywordValues = ["inherit", "initial", "unset", "revert", "none", "auto", "0", "50%"];

    private static readonly string[] _markupExtensions = [".razor", ".cshtml", ".html"];

    [Fact]
    public void MockTokensMatchTheDesignDocument()
    {
        string mock = File.ReadAllText(Path.Combine(RepoFiles.Root, DesignDocument.MockFile));
        List<string> differences = Differences(DesignDocument.Declarations(mock));
        Assert.True(differences.Count == 0, $"{DesignDocument.MockFile} and DESIGN.md section 7.4 differ: {string.Join("; ", differences)}");
    }

    [Fact]
    public void MockUsesTokensNotLiterals()
    {
        Match screen = MockScreen().Match(File.ReadAllText(Path.Combine(RepoFiles.Root, DesignDocument.MockFile)));
        Assert.True(screen.Success, $"{DesignDocument.MockFile} has no <template id=\"tpl\"> holding the screen.");
        List<string> offenders = [];
        foreach (Match attribute in StyleAttribute().Matches(screen.Value))
        {
            foreach (string declaration in attribute.Groups[1].Value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                int colon = declaration.IndexOf(':', StringComparison.Ordinal);
                string? problem = colon < 0 ? null : Problem(declaration[..colon].Trim(), declaration[(colon + 1)..].Trim());
                if (problem is not null)
                {
                    offenders.Add(problem);
                }
            }
        }
        Assert.True(offenders.Count == 0, $"{DesignDocument.MockFile} uses a literal where DESIGN.md section 7.4 has a token: {string.Join("; ", offenders.Distinct())}");
    }

    [Fact]
    public void StyleSheetTokensMatchTheDesignDocument()
    {
        if (!Directory.Exists(Path.Combine(RepoFiles.Root, "src", "AudioLab.Web")))
        {
            return;
        }
        string file = Path.Combine(RepoFiles.Root, DesignDocument.TokensFile);
        Assert.True(File.Exists(file), $"{DesignDocument.TokensFile} is missing; it declares the tokens in DESIGN.md section 7.4.");
        List<string> differences = Differences(DesignDocument.Declarations(File.ReadAllText(file)));
        Assert.True(differences.Count == 0, $"{DesignDocument.TokensFile} and DESIGN.md section 7.4 differ: {string.Join("; ", differences)}");
    }

    [Fact]
    public void TextTokensMeetTheContrastFloor()
    {
        List<string> surfaces = [.. DesignDocument.Tokens.Keys.Where(name => name.StartsWith("--surface-", StringComparison.Ordinal))];
        List<string> texts = [.. DesignDocument.Tokens.Keys.Where(name => name == "--text" || name.StartsWith("--text-", StringComparison.Ordinal))];
        List<string> fills = [.. _fillTokens, .. DesignDocument.Tokens.Keys.Where(name => name.StartsWith("--track-", StringComparison.Ordinal))];
        Assert.NotEmpty(surfaces);
        Assert.NotEmpty(texts);
        List<string> failures = [];
        foreach (string text in texts)
        {
            failures.AddRange(surfaces.Where(surface => Applies(text, surface)).Select(surface => Shortfall(text, surface)).OfType<string>());
        }
        failures.AddRange(fills.Select(fill => Shortfall("--ink", fill)).OfType<string>());
        Assert.True(failures.Count == 0, $"Token pairs under {TextContrastFloor}:1: {string.Join("; ", failures)}");
    }

    [Fact]
    public void StylesUseTokensNotLiterals()
    {
        List<string> offenders = [];
        foreach (string file in RepoFiles.WithExtensions(".css"))
        {
            string relative = RepoFiles.Relative(file);
            if (relative != DesignDocument.TokensFile)
            {
                string css = Comment().Replace(File.ReadAllText(file), string.Empty);
                offenders.AddRange(Declaration().Matches(css).Select(match => Problem(match.Groups[1].Value, match.Groups[2].Value.Trim())).OfType<string>().Select(problem => $"{relative}: {problem}"));
            }
        }
        foreach (string file in RepoFiles.WithExtensions(_markupExtensions))
        {
            foreach (Match attribute in StyleAttribute().Matches(File.ReadAllText(file)))
            {
                string value = attribute.Groups[1].Value;
                if (ColourLiteral().IsMatch(value) || ViewportUnit().IsMatch(value))
                {
                    offenders.Add($"{RepoFiles.Relative(file)}: style=\"{value}\"");
                }
            }
        }
        Assert.True(offenders.Count == 0, $"Styles that use a literal where DESIGN.md section 7.4 has a token, or a viewport unit: {string.Join("; ", offenders)}");
    }

    /// <summary>DESIGN.md keeps the faintest text off raised surfaces, so that pair is not held to the floor.</summary>
    private static bool Applies(string text, string surface)
    {
        return text != "--text-faint" || !surface.StartsWith("--surface-raised", StringComparison.Ordinal);
    }

    private static List<string> Differences(Dictionary<string, string> declared)
    {
        List<string> differences = [];
        foreach (KeyValuePair<string, string> token in DesignDocument.Tokens)
        {
            if (!declared.TryGetValue(token.Key, out string? value))
            {
                differences.Add($"{token.Key} is missing");
            }
            else if (value != token.Value)
            {
                differences.Add($"{token.Key} is '{value}', DESIGN.md says '{token.Value}'");
            }
        }
        differences.AddRange(declared.Keys.Where(name => !DesignDocument.Tokens.ContainsKey(name)).Select(name => $"{name} is not in DESIGN.md"));
        return differences;
    }

    private static string? Shortfall(string foreground, string background)
    {
        Assert.True(TokenColour.TryParse(DesignDocument.Tokens[foreground], out TokenColour front), $"{foreground} is not an opaque #rrggbb colour.");
        Assert.True(TokenColour.TryParse(DesignDocument.Tokens[background], out TokenColour back), $"{background} is not an opaque #rrggbb colour.");
        double ratio = TokenColour.Contrast(front, back);
        return ratio >= TextContrastFloor ? null : $"{foreground} on {background} is {ratio.ToString("0.00", CultureInfo.InvariantCulture)}:1";
    }

    private static string? Problem(string property, string value)
    {
        if (ColourLiteral().IsMatch(value))
        {
            return $"colour literal in '{property}: {value}'";
        }
        if (ViewportUnit().IsMatch(value))
        {
            return $"viewport unit in '{property}: {value}'";
        }
        bool tokenOnly = _tokenOnlyProperties.Contains(property) || (property.StartsWith("border", StringComparison.Ordinal) && property.EndsWith("radius", StringComparison.Ordinal));
        bool usesToken = value.Contains("var(--", StringComparison.Ordinal) || value.Contains("{{", StringComparison.Ordinal);
        if (tokenOnly && !usesToken && !_keywordValues.Contains(value))
        {
            return $"'{property}: {value}' is not a token";
        }
        return null;
    }

    [GeneratedRegex(@"/\*.*?\*/", RegexOptions.Singleline)]
    private static partial Regex Comment();

    [GeneratedRegex(@"(?<![\w-])([a-z-]+)\s*:\s*([^;{}]+)(?=[;}])")]
    private static partial Regex Declaration();

    [GeneratedRegex(@"\bstyle\s*=\s*""([^""]*)""", RegexOptions.IgnoreCase)]
    private static partial Regex StyleAttribute();

    [GeneratedRegex(@"<template id=""tpl"">.*?</template>", RegexOptions.Singleline)]
    private static partial Regex MockScreen();

    [GeneratedRegex(@"#[0-9a-fA-F]{3,8}\b|\b(rgb|rgba|hsl|hsla|hwb|lab|lch|oklab|oklch|color)\(", RegexOptions.IgnoreCase)]
    private static partial Regex ColourLiteral();

    [GeneratedRegex(@"\d(vh|vw|dvh|dvw|svh|svw|lvh|lvw|vmin|vmax)\b", RegexOptions.IgnoreCase)]
    private static partial Regex ViewportUnit();
}
