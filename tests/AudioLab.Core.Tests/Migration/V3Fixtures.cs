namespace AudioLab.Core.Tests.Migration;

/// <summary>Reads the version 3 fixtures. They are synthetic: written from the extension's source, not saved from the app.</summary>
public static class V3Fixtures
{
    /// <summary>A project that uses every part of version 3 and every case the migration repairs.</summary>
    public const string Full = "synthetic-v3-full.json";

    /// <summary>The smallest version 3 project.</summary>
    public const string Minimal = "synthetic-v3-minimal.json";

    public static string Read(string name)
    {
        return File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Migration", "Fixtures", name));
    }
}
