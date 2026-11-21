namespace AudioLab.Conventions.Tests;

/// <summary>A throwaway folder with src and tests, for tests of how projects are found.</summary>
internal sealed class TempRepository : IDisposable
{
    /// <summary>Creates the folder.</summary>
    public TempRepository()
    {
        Root = Path.Combine(Path.GetTempPath(), "audiolab-conventions-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
    }

    /// <summary>The folder that holds src and tests.</summary>
    public string Root { get; }

    /// <summary>Writes a project file, and its restore output when one is given.</summary>
    public void AddProject(string projectPath, string? assetsJson)
    {
        string project = Path.Combine(Root, projectPath);
        Directory.CreateDirectory(Path.GetDirectoryName(project)!);
        File.WriteAllText(project, "<Project />");
        if (assetsJson is not null)
        {
            AddAssets(Path.GetDirectoryName(projectPath)!, assetsJson);
        }
    }

    /// <summary>Writes AudioLab.slnx listing the given project paths.</summary>
    public void AddSolution(params string[] projects)
    {
        string entries = string.Concat(projects.Select(project => $"<Project Path=\"{project}\" />"));
        File.WriteAllText(Path.Combine(Root, "AudioLab.slnx"), $"<Solution>{entries}</Solution>");
    }

    /// <summary>Writes obj/project.assets.json under a folder, whether or not a project is there.</summary>
    public void AddAssets(string folder, string assetsJson)
    {
        string obj = Path.Combine(Root, folder, "obj");
        Directory.CreateDirectory(obj);
        File.WriteAllText(Path.Combine(obj, "project.assets.json"), assetsJson);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Directory.Delete(Root, recursive: true);
    }
}
