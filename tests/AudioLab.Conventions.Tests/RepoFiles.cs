using System.Xml.Linq;

namespace AudioLab.Conventions.Tests;

/// <summary>Finds the repository on disk and lists the source files the convention checks read.</summary>
public static class RepoFiles
{
    /// <summary>Absolute path of the repository root, the folder that holds the solution file.</summary>
    public static readonly string Root = FindRoot();

    private static readonly string[] _sourceFolders = ["src", "tests"];

    private static readonly string[] _skippedFolders = ["bin", "obj", "node_modules", "artifacts"];

    /// <summary>Every authored file under src and tests with one of the given extensions, as absolute paths.</summary>
    public static List<string> WithExtensions(params string[] extensions)
    {
        return WithExtensionsIn(Root, extensions);
    }

    /// <summary>Every authored file under the src and tests folders of the given root with one of the extensions, as absolute paths.</summary>
    public static List<string> WithExtensionsIn(string root, params string[] extensions)
    {
        List<string> found = [];
        foreach (string folder in _sourceFolders)
        {
            string path = Path.Combine(root, folder);
            if (Directory.Exists(path))
            {
                Collect(path, extensions, found);
            }
        }
        return found;
    }

    /// <summary>The projects in AudioLab.slnx under a root that the scan for .csproj files does not find, as the solution writes them.</summary>
    /// <remarks>A project in bin, obj, node_modules, artifacts or outside src and tests would be invisible to the package gate.</remarks>
    public static List<string> SolutionProjectsTheScanMisses(string root)
    {
        XDocument solution = XDocument.Load(Path.Combine(root, "AudioLab.slnx"));
        HashSet<string> scanned = [.. WithExtensionsIn(root, ".csproj").Select(file => Path.GetRelativePath(root, file).Replace('\\', '/'))];
        IEnumerable<string> listed = solution.Descendants("Project").Select(project => (string?)project.Attribute("Path") ?? string.Empty);
        return [.. listed.Select(path => path.Replace('\\', '/')).Where(path => !scanned.Contains(path))];
    }

    /// <summary>The repository-relative path of a file, with forward slashes on every OS.</summary>
    public static string Relative(string path)
    {
        return Path.GetRelativePath(Root, path).Replace('\\', '/');
    }

    private static void Collect(string folder, string[] extensions, List<string> found)
    {
        foreach (string file in Directory.EnumerateFiles(folder))
        {
            if (extensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase))
            {
                found.Add(file);
            }
        }
        foreach (string child in Directory.EnumerateDirectories(folder))
        {
            if (!_skippedFolders.Contains(Path.GetFileName(child), StringComparer.OrdinalIgnoreCase))
            {
                Collect(child, extensions, found);
            }
        }
    }

    private static string FindRoot()
    {
        DirectoryInfo? folder = new(AppContext.BaseDirectory);
        while (folder is not null && !File.Exists(Path.Combine(folder.FullName, "AudioLab.slnx")))
        {
            folder = folder.Parent;
        }
        return folder?.FullName ?? throw new InvalidOperationException("AudioLab.slnx was not found above the test output folder.");
    }
}
