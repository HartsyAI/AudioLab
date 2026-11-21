namespace AudioLab.Core.Tests.Migration;

/// <summary>Reads the version 3 fixtures. They are synthetic: written from the extension's source, not saved by it.</summary>
public static class V3Fixtures
{
    /// <summary>A project that uses every part of version 3 and every case the migration repairs.</summary>
    public const string Full = "synthetic-v3-full.json";

    /// <summary>The smallest version 3 project.</summary>
    public const string Minimal = "synthetic-v3-minimal.json";

    /// <summary>A project from before audio moved to its own files: the audio is inside the JSON.</summary>
    public const string Inline = "synthetic-v3-inline.json";

    /// <summary>A project in the autosave format: no audio list, the audio lives elsewhere.</summary>
    public const string Autosave = "synthetic-v3-autosave.json";

    public static string Read(string name)
    {
        return File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Migration", "Fixtures", name));
    }
}
