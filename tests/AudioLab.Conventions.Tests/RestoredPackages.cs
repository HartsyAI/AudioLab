using System.Text.Json;

namespace AudioLab.Conventions.Tests;

/// <summary>Lists the NuGet packages that the projects under src and tests restored, from their project.assets.json files.</summary>
public static class RestoredPackages
{
    private const string AssetsFileName = "project.assets.json";

    private static readonly string[] _projectFolders = ["src", "tests"];

    /// <summary>Every package, with its file list, that any project restored, transitive packages included.</summary>
    public static List<PackageListing> Read()
    {
        List<string> files = AssetsFiles();
        if (files.Count == 0)
        {
            throw new InvalidOperationException($"No obj/{AssetsFileName} was found under src or tests; run 'dotnet restore' first.");
        }
        Dictionary<string, PackageListing> found = [];
        foreach (string file in files)
        {
            AssetsDocument assets = JsonSerializer.Deserialize(File.ReadAllText(file), AssetsJsonContext.Default.AssetsDocument)
                ?? throw new InvalidOperationException($"{RepoFiles.Relative(file)} is empty.");
            foreach (KeyValuePair<string, AssetsLibrary> library in assets.Libraries ?? [])
            {
                int slash = library.Key.IndexOf('/', StringComparison.Ordinal);
                if (library.Value.Type == "package" && slash > 0)
                {
                    found.TryAdd(library.Key, new PackageListing(library.Key[..slash], library.Key[(slash + 1)..], library.Value.Files ?? []));
                }
            }
        }
        return [.. found.Values.OrderBy(package => package.Id, StringComparer.OrdinalIgnoreCase).ThenBy(package => package.Version, StringComparer.Ordinal)];
    }

    private static List<string> AssetsFiles()
    {
        List<string> files = [];
        foreach (string folder in _projectFolders)
        {
            string path = Path.Combine(RepoFiles.Root, folder);
            if (Directory.Exists(path))
            {
                files.AddRange(Directory.EnumerateDirectories(path).Select(project => Path.Combine(project, "obj", AssetsFileName)).Where(File.Exists));
            }
        }
        return files;
    }
}
