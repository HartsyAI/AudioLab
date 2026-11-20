using System.Text.RegularExpressions;

namespace AudioLab.Conventions.Tests;

/// <summary>Reads the design tokens that DESIGN.md section 7.4 declares, the authority for every style value.</summary>
public static partial class DesignDocument
{
    /// <summary>Repository-relative path of the style sheet that must declare exactly the tokens in DESIGN.md.</summary>
    public const string TokensFile = "src/AudioLab.Web/wwwroot/css/tokens.css";

    /// <summary>Repository-relative path of the session mock, the picture of the design, which declares the same tokens.</summary>
    public const string MockFile = "docs/design/session-mock.html";

    private const string StartMarker = "<!-- tokens:start -->";

    private const string EndMarker = "<!-- tokens:end -->";

    /// <summary>Token name to value, as written between the token markers in DESIGN.md.</summary>
    public static readonly Dictionary<string, string> Tokens = ReadTokens();

    /// <summary>Every custom-property declaration in a piece of CSS, name to value with whitespace collapsed.</summary>
    public static Dictionary<string, string> Declarations(string css)
    {
        Dictionary<string, string> found = [];
        foreach (Match match in CustomProperty().Matches(css))
        {
            string name = match.Groups[1].Value;
            string value = Whitespace().Replace(match.Groups[2].Value.Trim(), " ");
            if (!found.TryAdd(name, value))
            {
                throw new InvalidOperationException($"The token {name} is declared twice.");
            }
        }
        return found;
    }

    private static Dictionary<string, string> ReadTokens()
    {
        string document = File.ReadAllText(Path.Combine(RepoFiles.Root, "DESIGN.md"));
        int start = document.IndexOf(StartMarker, StringComparison.Ordinal);
        int end = document.IndexOf(EndMarker, StringComparison.Ordinal);
        if (start < 0 || end < start)
        {
            throw new InvalidOperationException("DESIGN.md has no token block between its tokens:start and tokens:end markers.");
        }
        return Declarations(document[start..end]);
    }

    [GeneratedRegex(@"^\s*(--[a-z0-9-]+)\s*:\s*([^;]+);", RegexOptions.Multiline)]
    private static partial Regex CustomProperty();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
