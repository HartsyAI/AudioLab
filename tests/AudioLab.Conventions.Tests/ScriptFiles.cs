namespace AudioLab.Conventions.Tests;

/// <summary>Which files count as JavaScript or TypeScript, shared by every script rule. HTML is not a script file here.</summary>
public static class ScriptFiles
{
    /// <summary>File extensions of script files, with the leading dot.</summary>
    public static readonly string[] Extensions = [".js", ".mjs", ".cjs", ".jsx", ".ts", ".mts", ".cts", ".tsx"];

    /// <summary>True when the path, with either kind of slash, names a script file. Trailing dots and spaces are ignored, as on Windows.</summary>
    public static bool IsScript(string path)
    {
        return Extensions.Contains(Path.GetExtension(path.TrimEnd('.', ' ')), StringComparer.OrdinalIgnoreCase);
    }
}
