namespace AudioLab.Conventions.Tests;

/// <summary>Which files count as JavaScript or TypeScript, shared by every script rule.</summary>
public static class ScriptFiles
{
    /// <summary>File extensions of script files, with the leading dot.</summary>
    public static readonly string[] Extensions = [".js", ".mjs", ".cjs", ".jsx", ".ts", ".tsx"];

    /// <summary>True when the path, written with either kind of slash, names a script file.</summary>
    public static bool IsScript(string path)
    {
        return Extensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);
    }
}
