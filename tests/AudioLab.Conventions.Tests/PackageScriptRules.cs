namespace AudioLab.Conventions.Tests;

/// <summary>The decisions behind the package half of the JavaScript policy, free of the file system so tests can feed them any listing.</summary>
public static class PackageScriptRules
{
    /// <summary>One line for each restored package that ships a script file and whose id is not on the allowlist.</summary>
    public static List<string> Unlisted(IEnumerable<PackageListing> restored, IEnumerable<PackageScriptAllowance> allowed)
    {
        HashSet<string> listed = new(allowed.Select(entry => entry.PackageId), StringComparer.OrdinalIgnoreCase);
        List<string> unlisted = [];
        foreach (PackageListing package in restored.Where(package => !listed.Contains(package.Id)))
        {
            List<string> scripts = [.. package.Files.Where(ScriptFiles.IsScript)];
            if (scripts.Count > 0)
            {
                string projects = string.Join(", ", package.Projects);
                string fix = $"Add new(\"{package.Id}\", \"why C# cannot do it\") to ScriptAllowlist.Packages, or drop the package.";
                string shipped = $"ships {scripts.Count} script file(s): {string.Join(", ", scripts)}";
                unlisted.Add($"{package.Id} {package.Version} (restored by {projects}) {shipped}. {fix}");
            }
        }
        return unlisted;
    }

    /// <summary>One line for each allowlist entry that is blank, repeated, not restored, or ships no script in any restored version.</summary>
    public static List<string> EntryProblems(IEnumerable<PackageListing> restored, IEnumerable<PackageScriptAllowance> allowed)
    {
        List<PackageListing> packages = [.. restored];
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        List<string> problems = [];
        foreach (PackageScriptAllowance entry in allowed)
        {
            if (string.IsNullOrWhiteSpace(entry.PackageId) || string.IsNullOrWhiteSpace(entry.Reason))
            {
                problems.Add($"'{entry.PackageId}' needs a package id and a reason");
            }
            else if (!seen.Add(entry.PackageId))
            {
                problems.Add($"{entry.PackageId} is listed twice");
            }
            else
            {
                List<PackageListing> matches = [.. packages.Where(package => IsSameId(package.Id, entry.PackageId))];
                if (matches.Count == 0)
                {
                    problems.Add($"{entry.PackageId} is not restored by any project (remove the entry, or reference the package)");
                }
                else if (!matches.Any(package => package.Files.Any(ScriptFiles.IsScript)))
                {
                    problems.Add($"{entry.PackageId} ships no script file (remove the entry)");
                }
            }
        }
        return problems;
    }

    private static bool IsSameId(string left, string right)
    {
        return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    }
}
