using System.Text.Json;

namespace AudioLab.Conventions.Tests;

/// <summary>Lists the NuGet packages that the projects under src and tests restored, from their project.assets.json files.</summary>
public static class RestoredPackages
{
    private const string AssetsFileName = "project.assets.json";

    private static readonly int[] _supportedVersions = [3, 4];

    /// <summary>Every package, with its file list, that any project of this repository restored, transitive packages included.</summary>
    /// <exception cref="InvalidOperationException">No project was found, or one has no restore output or an unreadable one.</exception>
    public static List<PackageListing> Read()
    {
        return Read(RepoFiles.Root);
    }

    /// <summary>Every package, with its file list, that any project under the src and tests folders of a root restored.</summary>
    /// <param name="root">The folder that holds src and tests.</param>
    /// <exception cref="InvalidOperationException">No project was found, or one has no restore output or an unreadable one.</exception>
    public static List<PackageListing> Read(string root)
    {
        List<string> projects = RepoFiles.WithExtensionsIn(root, ".csproj");
        if (projects.Count == 0)
        {
            throw new InvalidOperationException("No .csproj was found under src or tests, so there is nothing to read packages from.");
        }
        Dictionary<string, PackageListing> found = [];
        foreach (string project in projects)
        {
            string name = Path.GetRelativePath(root, project).Replace('\\', '/');
            string assets = Path.Combine(Path.GetDirectoryName(project)!, "obj", AssetsFileName);
            if (!File.Exists(assets))
            {
                throw new InvalidOperationException(
                    $"{name} has no obj/{AssetsFileName}; run 'dotnet restore' at the repository root. The project must be in AudioLab.slnx.");
            }
            foreach (PackageListing package in Parse(File.ReadAllText(assets), name))
            {
                string key = $"{package.Id}/{package.Version}";
                found[key] = found.TryGetValue(key, out PackageListing? earlier) ? earlier with { Projects = [.. earlier.Projects, name] } : package;
            }
        }
        IOrderedEnumerable<PackageListing> byId = found.Values.OrderBy(package => package.Id, StringComparer.OrdinalIgnoreCase);
        return [.. byId.ThenBy(package => package.Version, StringComparer.Ordinal)];
    }

    /// <summary>The packages a project.assets.json lists. Fails on anything it cannot read, so the gate never passes on a guess.</summary>
    /// <param name="json">The content of the assets file.</param>
    /// <param name="project">The project it belongs to, for messages and for <see cref="PackageListing.Projects"/>.</param>
    public static List<PackageListing> Parse(string json, string project)
    {
        AssetsDocument assets = JsonSerializer.Deserialize(json, AssetsJsonContext.Default.AssetsDocument)
            ?? throw new InvalidOperationException($"The restore output of {project} is empty.");
        if (!_supportedVersions.Contains(assets.Version))
        {
            string known = string.Join(" and ", _supportedVersions);
            throw new InvalidOperationException($"The restore output of {project} is version {assets.Version}; this reader knows {known}.");
        }
        if (assets.Libraries is null)
        {
            throw new InvalidOperationException($"The restore output of {project} has no libraries section.");
        }
        List<PackageListing> packages = [];
        foreach (KeyValuePair<string, AssetsLibrary?> library in assets.Libraries)
        {
            AssetsLibrary entry = library.Value
                ?? throw new InvalidOperationException($"The restore output of {project} has no content for '{library.Key}'.");
            if (entry.Type == "project")
            {
                continue;
            }
            if (entry.Type != "package")
            {
                string kind = $"'{entry.Type}', not a package or project";
                throw new InvalidOperationException($"The restore output of {project} lists '{library.Key}' as {kind}.");
            }
            packages.Add(ToListing(library.Key, entry, project));
        }
        return packages;
    }

    private static PackageListing ToListing(string key, AssetsLibrary entry, string project)
    {
        int slash = key.IndexOf('/', StringComparison.Ordinal);
        if (slash <= 0)
        {
            throw new InvalidOperationException($"The restore output of {project} lists '{key}' without an id and a version.");
        }
        List<string> files = entry.Files
            ?? throw new InvalidOperationException($"The restore output of {project} lists no files for the package {key}.");
        return new PackageListing(key[..slash], key[(slash + 1)..], files, [project]);
    }
}
