namespace AudioLab.Web.Tests;

/// <summary>Reads a text file of the repository by its path from the root, for a test that holds a style rule the markup cannot show.</summary>
internal static class RepoText
{
    /// <summary>The text of a file such as <c>src/AudioLab.Web/Components/Clip.razor.css</c>.</summary>
    public static string Read(string relativePath)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AudioLab.slnx")))
        {
            directory = directory.Parent;
        }
        return directory is null
            ? throw new InvalidOperationException("The repository root was not found above the test output.")
            : File.ReadAllText(Path.Combine(directory.FullName, relativePath));
    }
}
